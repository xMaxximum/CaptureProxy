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
        private Uri? baseUri;

        public async Task StartAsync()
        {
#if DEBUG
            proxy.Events.Log($"Session start for {client.IpPort}.");
#endif

            try
            {
                await HandleTunneling().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                proxy.Events.Log("Handling tunnel to " + (baseUri?.Authority ?? "undefined"));
                proxy.Events.Log(ex);
            }

            proxy.Events.HandleSessionDisconnected(this, new SessionDisconnectedEventArgs(this));

#if DEBUG
            proxy.Events.Log($"Session stop for {client.IpPort}.");
#endif
        }

        public void Dispose()
        {
            remote?.Close();
            remote?.Dispose();

            client.Close();
            client.Dispose();

            // Clean up resources
            GC.SuppressFinalize(this);
        }

        private async Task HandleTunneling()
        {
            // Handle first request
            var request = new HttpRequest(proxy);
            await request.ReadHeaderAsync(client).ConfigureAwait(false);

            // BaseURL
            baseUri = new Uri($"{request.Uri.Scheme}://{request.Uri.Authority}");

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
            }
            else
            {
                await new BufferTunnel(config).StartAsync().ConfigureAwait(false);
            }
        }

        private async Task<Client?> EstablishRemote(HttpRequest request, BeforeTunnelEstablishEventArgs e)
        {
            var originalRemote = $"{e.Host}:{e.Port}";

            proxy.Events.HandleBeforeTunnelEstablish(this, e);

            var updatedRemote = $"{e.Host}:{e.Port}";
            var upstreamProxy = e.UpstreamProxy != null ? ($"{e.UpstreamProxy.Host}:{e.UpstreamProxy.Port}") : null;

            if (e.Abort) return null;

            try
            {
                var remoteHost = e.UpstreamProxy?.Host ?? e.Host;
                var remotePort = e.UpstreamProxy?.Port ?? e.Port;

                var remote = new TcpClient();
                await remote.ConnectAsync(remoteHost, remotePort).ConfigureAwait(false);

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
                proxy.Events.Log($"Original remote: {originalRemote}");
                proxy.Events.Log($"Updated remote: {updatedRemote}");
                proxy.Events.Log($"Upstream proxy: {upstreamProxy}");
                proxy.Events.Log(ex);
                return null;
            }
        }
    }
}
