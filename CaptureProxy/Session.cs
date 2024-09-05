using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using CaptureProxy.Tunnels;
using System;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class Session : IDisposable
    {
        public static List<Session> Collection { get; } = [];

        private Client _client;
        private Client? _remote;
        private bool _isDisposing = false;

        public Session(Client client)
        {
            _client = client;
            Collection.Add(this);
        }

        public async Task StartAsync()
        {
#if DEBUG
            Events.Log($"Session start for {_client.IpPort}.");
#endif

            await HandleTunneling();

            Dispose();
        }

        private void Stop()
        {
            if (_remote != null)
            {
                while (_remote.Connected)
                {
                    _remote.Close();
                    _remote.Dispose();
                }
            }

            while (_client.Connected)
            {
                _client.Close();
                _client.Dispose();
            }
        }

        public void Dispose()
        {
            if (_isDisposing) return;
            _isDisposing = true;

            Stop();

            lock (Collection)
            {
                Collection.Remove(this);
            }

            Events.HandleSessionDisconnected(this, new SessionDisconnectedEventArgs(this));

#if DEBUG
            Events.Log($"Session stop for {_client.IpPort}.");
#endif
        }

        private async Task HandleTunneling()
        {
            try
            {
                // Handle first request
                using var request = new HttpRequest();
                await request.ReadHeaderAsync(_client);

                // BaseURL
                var baseUri = new Uri($"{request.Uri.Scheme}://{request.Uri.Authority}");

                // Khởi tạo kết nối tới địa chỉ đích
                var e = new BeforeTunnelEstablishEventArgs(baseUri.Host, baseUri.Port);
                _remote = await EstablishRemote(request, e);

                // Trả về packet lỗi nếu không thể khởi tạo kết nối tới địa chỉ đích
                if (_remote == null)
                {
                    await Helper.SendBadGatewayResponse(_client);
                    return;
                }

                // Start tunnel
                var config = new TunnelConfiguration
                {
                    BaseUri = baseUri,
                    Client = _client,
                    Remote = _remote,
                    e = e,
                    InitRequest = request,
                };

                if (e.PacketCapture || request.Method != HttpMethod.Connect)
                {
                    await new DecryptedTunnel(config).StartAsync();
                    return;
                }

                await new BufferTunnel(config).StartAsync();
            }
            catch (Exception ex)
            {
                if (_isDisposing) return;
                Events.Log(ex);
            }
        }

        private async Task<Client?> EstablishRemote(HttpRequest request, BeforeTunnelEstablishEventArgs e)
        {
            try
            {
                Events.HandleBeforeTunnelEstablish(this, e);

                if (e.Abort) return null;

                var remote = new TcpClient();
                await remote.ConnectAsync(e.Host, e.Port);

                if (remote.Connected == false)
                {
                    remote.Close();
                    remote.Dispose();
                    return null;
                }

                // Store remote client
                return new Client(remote);
            }
            catch (Exception ex)
            {
                Events.Log(ex);
                return null;
            }
        }

        private async Task SendBlockedResponse(HttpRequest request)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = request.Version;
            response.StatusCode = HttpStatusCode.BadRequest;
            response.ReasonPhrase = "Bad Request";
            response.SetBody("[CaptureProxy] The hostname cannot be resolved or it has been blocked.");
            await response.WriteHeaderAsync(_client);
            await response.WriteBodyAsync(_client);
        }

        public static void DisposeAll()
        {
            Parallel.ForEach(Collection.ToList(), x => x.Dispose());
        }
    }
}
