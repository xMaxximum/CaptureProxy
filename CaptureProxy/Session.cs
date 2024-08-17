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
        private bool _useSSL = false;
        private Client? _remote;
        private Uri? _baseUri;
        private BeforeTunnelEstablishEventArgs? _tunnelEstablishEvent;
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

                // SSL or not
                _useSSL = request.Method == HttpMethod.Connect;

                // BaseURL
                _baseUri = new Uri($"{request.Uri.Scheme}://{request.Uri.Authority}");

                // Send connect response
                if (request.Method == HttpMethod.Connect)
                {
                    await SendConnectResponse(request);
                }

                // Khởi tạo kết nối tới địa chỉ đích
                _remote = await EstablishRemote(request);

                // Trả về packet lỗi nếu không thể khởi tạo kết nối tới địa chỉ đích
                if (_remote == null) return;

                // Chuyển tiếp dữ liệu mà không giải mã chúng
                if (_tunnelEstablishEvent?.PacketCapture == false)
                {
                    await new BufferTunnel(new TunnelConfiguration
                    {
                        BaseUri = _baseUri,
                        Client = _client,
                        Remote = _remote,
                        TunnelEstablishEvent = _tunnelEstablishEvent,
                        InitRequest = !_useSSL ? request : null,
                        UseSSL = _useSSL,
                    }).StartAsync();
                }
                else
                {
                    // SSL Authenticate if needed
                    if (_useSSL)
                    {
                        _client.AuthenticateAsServer(_baseUri.Host);
                        _remote.AuthenticateAsClient(_baseUri.Host);
                    }

                    // Start tunnel
                    await new DecryptedTunnel(new TunnelConfiguration
                    {
                        BaseUri = _baseUri,
                        Client = _client,
                        Remote = _remote,
                        TunnelEstablishEvent = _tunnelEstablishEvent,
                        InitRequest = !_useSSL ? request : null,
                        UseSSL = _useSSL,
                    }).StartAsync();
                }
            }
            catch (Exception ex)
            {
                if (_isDisposing) return;
                Events.Log(ex);
            }
        }

        private async Task SendConnectResponse(HttpRequest request)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = request.Version;
            response.StatusCode = HttpStatusCode.OK;
            response.ReasonPhrase = "Connection Established";
            await response.WriteHeaderAsync(_client);
        }

        private async Task<Client?> EstablishRemote(HttpRequest request)
        {
            if (_baseUri == null) return null;

            try
            {
                _tunnelEstablishEvent = new BeforeTunnelEstablishEventArgs(_baseUri.Host, _baseUri.Port);
                Events.HandleBeforeTunnelEstablish(this, _tunnelEstablishEvent);

                if (_tunnelEstablishEvent.Abort) return null;

                var remote = new TcpClient();
                for (int i = 0; i < 3; i++)
                {
                    if (!Settings.ProxyIsRunning) break;

                    try
                    {
                        await remote.ConnectAsync(_tunnelEstablishEvent.Host, _tunnelEstablishEvent.Port);
                        break;
                    }
                    catch
                    {
                        Events.Log($"Cannot create tunnel to {_tunnelEstablishEvent.Host}:{_tunnelEstablishEvent.Port} on {i + 1} tries.");
                    }
                }

                if (remote.Connected == false)
                {
                    remote.Close();
                    remote.Dispose();
                    return null;
                }

                // Store remote client
                var remoteClient = new Client(remote);

                if (_tunnelEstablishEvent.UpstreamProxy)
                {
                    using var connectRequest = new HttpRequest();
                    connectRequest.Method = HttpMethod.Connect;
                    connectRequest.Version = request.Version;
                    connectRequest.Uri = request.Uri;
                    if (_tunnelEstablishEvent.ProxyUser != null && _tunnelEstablishEvent.ProxyPass != null)
                    {
                        connectRequest.Headers.SetProxyAuthorization(_tunnelEstablishEvent.ProxyUser, _tunnelEstablishEvent.ProxyPass);
                    }
                    await connectRequest.WriteHeaderAsync(remoteClient);

                    using var connectResponse = new HttpResponse();
                    await connectResponse.ReadHeaderAsync(remoteClient);
                    if (connectResponse.StatusCode != HttpStatusCode.OK)
                    {
                        remoteClient.Close();
                        remoteClient.Dispose();
                        return null;
                    }
                }

                return remoteClient;
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
