using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using CaptureProxy.Tunnels;
using System;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class Session(HttpProxy proxy, Client client) : IDisposable
    {
        private Client? remote;
        private bool isDisposing = false;

        public async Task StartAsync()
        {
#if DEBUG
            proxy.Events.Log($"Session start for {client.IpPort}.");
#endif

            await HandleTunneling().ConfigureAwait(false);

            Dispose();
        }

        private void Stop()
        {
            if (remote != null)
            {
                while (remote.Connected)
                {
                    remote.Close();
                    remote.Dispose();
                }
            }

            while (client.Connected)
            {
                client.Close();
                client.Dispose();
            }
        }

        public void Dispose()
        {
            if (isDisposing) return;
            isDisposing = true;

            Stop();

            proxy.Events.HandleSessionDisconnected(this, new SessionDisconnectedEventArgs(this));

#if DEBUG
            proxy.Events.Log($"Session stop for {client.IpPort}.");
#endif
        }

        private async Task HandleTunneling()
        {
            try
            {
                // Handle first request
                using var request = new HttpRequest(proxy);
                await request.ReadHeaderAsync(client).ConfigureAwait(false);

                // BaseURL
                var baseUri = new Uri($"{request.Uri.Scheme}://{request.Uri.Authority}");

                // Khởi tạo kết nối tới địa chỉ đích
                var e = new BeforeTunnelEstablishEventArgs(baseUri.Host, baseUri.Port);
                remote = await EstablishRemote(request, e).ConfigureAwait(false);

                // Trả về packet lỗi nếu không thể khởi tạo kết nối tới địa chỉ đích
                if (remote == null)
                {
                    await Helper.SendBadGatewayResponse(proxy, client).ConfigureAwait(false);
                    return;
                }

                // Start tunnel
                var config = new TunnelConfiguration
                {
                    Proxy = proxy,
                    BaseUri = baseUri,
                    Client = client,
                    Remote = remote,
                    e = e,
                    InitRequest = request,
                };

                if (e.PacketCapture || request.Method != HttpMethod.Connect)
                {
                    await new DecryptedTunnel(config).StartAsync().ConfigureAwait(false);
                    return;
                }

                await new BufferTunnel(config).StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (isDisposing) return;
                proxy.Events.Log(ex);
            }
        }

        private async Task<Client?> EstablishRemote(HttpRequest request, BeforeTunnelEstablishEventArgs e)
        {
            try
            {
                proxy.Events.HandleBeforeTunnelEstablish(this, e);

                if (e.Abort) return null;

                var remote = new TcpClient();
                await remote.ConnectAsync(e.Host, e.Port).ConfigureAwait(false);

                if (remote.Connected == false)
                {
                    remote.Close();
                    remote.Dispose();
                    return null;
                }

                // Store remote client
                return new Client(proxy, remote);
            }
            catch (Exception ex)
            {
                proxy.Events.Log(ex);
                return null;
            }
        }
    }
}
