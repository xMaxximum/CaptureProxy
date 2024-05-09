using CaptureProxy.MyEventArgs;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class Session : IDisposable
    {
        private CancellationToken _outsideToken;
        private CancellationTokenSource _tokenSrc = new CancellationTokenSource();
        private Tunnel? _tunnel;
        private Client _client;
        private bool _useSSL = false;
        private Client? _remote;
        private string baseUrl = string.Empty;
        private BeforeTunnelConnectEventArgs? tunnelEvent;

        public Session(Client client, CancellationToken outsideToken)
        {
            _client = client;
            _outsideToken = outsideToken;
        }

        public async Task StartAsync()
        {
#if DEBUG
            Events.Log($"Session start for {_client.IpPort}.");
#endif

            await Task.WhenAll([
                HandleOutsideToken(),
                HandleMainTask(),
            ]);

            _remote?.Close();
            _client.Close();

            Events.HandleSessionDisconnected(this, new SessionDisconnectedEventArgs(this));
#if DEBUG
            Events.Log($"Session stop for {_client.IpPort}.");
#endif
        }

        public void Stop()
        {
            _tokenSrc.Cancel();
            _tunnel?.Stop();
        }

        public void Dispose()
        {
            Stop();

            _tokenSrc.Dispose();
            _tunnel?.Dispose();
            _remote?.Dispose();
            _client.Dispose();
        }

        private async Task HandleOutsideToken()
        {
            while (true)
            {
                if (_outsideToken.IsCancellationRequested) break;
                if (_tokenSrc.Token.IsCancellationRequested) break;
                await Task.Delay(50).ConfigureAwait(false);
            }

            Stop();
        }

        private async Task HandleMainTask()
        {
            try
            {
                // Handle first request
                using HttpRequest request = new HttpRequest();
                await request.ReadHeaderAsync(_client.Stream, baseUrl, _tokenSrc.Token).ConfigureAwait(false);

                // SSL or not
                _useSSL = request.Method == HttpMethod.Connect;

                // BaseURL
                baseUrl = (_useSSL ? "https" : "http") + "://" + request.RequestUri.Host + ":" + request.RequestUri.Port;

                // Send connect response
                if (request.Method == HttpMethod.Connect)
                {
                    await SendConnectResponse(request).ConfigureAwait(false);
                }

                // Khởi tạo kết nối tới địa chỉ đích
                await EstablishRemote(request).ConfigureAwait(false);

                // Trả về packet lỗi nếu không thể khởi tạo kết nối tới địa chỉ đích
                if (_remote == null)
                {
                    // SSL Authenticate for client if needed
                    if (_useSSL)
                    {
                        _client.AuthenticateAsServer(request.RequestUri.Host);
                    }
                    await SendBlockedResponse(request).ConfigureAwait(false);
                    return;
                }

                // Sử dụng DecryptedTunnel với HTTP request
                // vì cần add thêm authorization header
                if (!_useSSL || tunnelEvent?.PacketCapture == true)
                {
                    // SSL Authenticate if needed
                    if (_useSSL)
                    {
                        _client.AuthenticateAsServer(request.RequestUri.Host);
                        _remote.AuthenticateAsClient(request.RequestUri.Host);
                    }

                    // Giải mã rồi chuyển tiếp dữ liệu
                    _tunnel = new DecryptedTunnel(_client, _remote, baseUrl, tunnelEvent);
                    _tunnel.RequestHeader = !_useSSL ? request : null;
                    await _tunnel.StartAsync().ConfigureAwait(false);
                }
                else
                {
                    // Chuyển tiếp dữ liệu mà không giải mã chúng
                    _tunnel = new BufferTunnel(_client, _remote);
                    _tunnel.RequestHeader = !_useSSL ? request : null;
                    await _tunnel.StartAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }
            finally
            {
                Stop();
            }
        }

        private async Task SendConnectResponse(HttpRequest request)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = request.Version;
            response.StatusCode = HttpStatusCode.OK;
            response.ReasonPhrase = "Connection Established";
            await response.WriteHeaderAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
        }

        private async Task EstablishRemote(HttpRequest request)
        {
            try
            {
                tunnelEvent = new BeforeTunnelConnectEventArgs(request.RequestUri.Host, request.RequestUri.Port);
                Events.HandleBeforeTunnelConnect(this, tunnelEvent);

                if (tunnelEvent.Abort) return;

                TcpClient remote = new TcpClient();
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        await remote.ConnectAsync(tunnelEvent.Host, tunnelEvent.Port, _tokenSrc.Token).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception)
                    {
                        Events.Log($"Cannot create tunnel to {tunnelEvent.Host}:{tunnelEvent.Port} on {i + 1} tries.");
                    }
                }

                if (remote.Connected == false)
                {
                    remote.Close();
                    remote.Dispose();
                    return;
                }

                if (tunnelEvent.UpstreamProxy)
                {
                    using HttpRequest connectRequest = new HttpRequest();
                    connectRequest.Method = HttpMethod.Connect;
                    connectRequest.Version = request.Version;
                    connectRequest.RequestUri = new Uri(baseUrl);
                    if (tunnelEvent.ProxyUser != null && tunnelEvent.ProxyPass != null)
                    {
                        connectRequest.Headers.SetProxyAuthorization(tunnelEvent.ProxyUser, tunnelEvent.ProxyPass);
                    }
                    await connectRequest.WriteHeaderAsync(remote.GetStream(), _tokenSrc.Token).ConfigureAwait(false);

                    using HttpResponse connectResponse = new HttpResponse();
                    await connectResponse.ReadHeaderAsync(remote.GetStream(), _tokenSrc.Token).ConfigureAwait(false);
                    if (connectResponse.StatusCode != HttpStatusCode.OK)
                    {
                        remote.Close();
                        remote.Dispose();
                        return;
                    }
                }

                // Store remote client
                _remote = new Client(remote);
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }
        }

        private async Task SendBlockedResponse(HttpRequest request)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = request.Version;
            response.StatusCode = HttpStatusCode.BadRequest;
            response.ReasonPhrase = "Bad Request";
            response.SetBody("[CaptureProxy] The hostname cannot be resolved or it has been blocked.");
            await response.WriteHeaderAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
            await response.WriteBodyAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
        }
    }
}
