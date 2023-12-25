using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    internal class Session : IDisposable
    {
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private Client _client;
        private bool _useSSL = false;
        private Client? _remote;
        private string baseUrl = string.Empty;

        public Session(Client client)
        {
            _client = client;
        }

        public async Task StartAsync()
        {
            Events.Log($"Session start for {_client.IpPort}.");

            // Handle first request
            using HttpRequest request = new HttpRequest();
            await request.ReadHeaderAsync(_client.Stream, baseUrl, _tokenSource.Token).ConfigureAwait(false);

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

            // SSL Authenticate if needed
            if (_useSSL)
            {
                _client.AuthenticateAsServer(request.RequestUri.Host);
                _remote.AuthenticateAsClient(request.RequestUri.Host);
            }

            if (true)
            {
                // Giải mã rồi chuyển tiếp dữ liệu
                DecryptedTunnel tunnel = new DecryptedTunnel(_client, _remote, baseUrl, _tokenSource.Token);
                await tunnel.StartAsync().ConfigureAwait(false);
            }
            else
            {
                // Chuyển tiếp dữ liệu mà không giải mã chúng
                BufferTunnel tunnel = new BufferTunnel(_client, _remote, _tokenSource.Token);
                tunnel.RequestHeader = !_useSSL ? request : null;
                await tunnel.StartAsync().ConfigureAwait(false);
            }
        }

        public void Stop()
        {
            Events.Log($"Session stop for {_client.IpPort}.");

            _tokenSource.Cancel();
            _remote?.Close();
            _client.Close();
        }

        public void Dispose()
        {
            Stop();

            _tokenSource.Dispose();
            _remote?.Dispose();
            _client.Dispose();
        }

        private async Task SendConnectResponse(HttpRequest request)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = request.Version;
            response.StatusCode = HttpStatusCode.OK;
            response.ReasonPhrase = "Connection Established";
            await response.WriteHeaderAsync(_client.Stream, _tokenSource.Token).ConfigureAwait(false);
        }

        private async Task EstablishRemote(HttpRequest request)
        {
            try
            {
                TcpClient remote = new TcpClient();
                await remote.ConnectAsync(request.RequestUri.Host, request.RequestUri.Port, _tokenSource.Token).ConfigureAwait(false);

                // Store remote client
                _remote = new Client(remote);
            }
            catch (Exception ex)
            {
                Events.Log(ex.Message);
            }
        }

        private async Task SendBlockedResponse(HttpRequest request)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = request.Version;
            response.StatusCode = HttpStatusCode.BadRequest;
            response.ReasonPhrase = "Bad Request";
            response.SetBody("[CaptureProxy] The hostname cannot be resolved or it has been blocked.");
            await response.WriteHeaderAsync(_client.Stream, _tokenSource.Token).ConfigureAwait(false);
            await response.WriteBodyAsync(_client.Stream, _tokenSource.Token).ConfigureAwait(false);
        }
    }
}