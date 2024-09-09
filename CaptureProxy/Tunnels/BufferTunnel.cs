using CaptureProxy.HttpIO;
using System;
using System.Net;

namespace CaptureProxy.Tunnels
{
    internal class BufferTunnel(TunnelConfiguration configuration)
    {
        private readonly CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(configuration.Proxy.Token);

        public async Task StartAsync()
        {
            // Connect to upstream proxy if needed
            if (configuration.e.UpstreamProxy != null)
            {
                var request = configuration.InitRequest;
                await request.ReadBodyAsync(configuration.Client).ConfigureAwait(false);

                // Sử dụng authentication có sẵn nếu được cung cấp
                if (configuration.e.UpstreamProxy.User != null && configuration.e.UpstreamProxy.Pass != null)
                {
                    request.Headers.SetProxyAuthorization(configuration.e.UpstreamProxy.User, configuration.e.UpstreamProxy.Pass);
                }

                // Chuyển tiếp request tới upstream proxy
                await request.WriteHeaderAsync(configuration.Remote, true).ConfigureAwait(false);
                await request.WriteBodyAsync(configuration.Remote).ConfigureAwait(false);

                // Đọc dữ liệu từ upstream proxy
                var response = new HttpResponse(configuration.Proxy);
                await response.ReadHeaderAsync(configuration.Remote).ConfigureAwait(false);
                await response.ReadBodyAsync(configuration.Remote).ConfigureAwait(false);

                // Nếu proxy vẫn chưa xác thực thành công thì dừng lại
                if (response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    await Helper.SendBadGatewayResponse(configuration.Proxy, configuration.Client).ConfigureAwait(false);
                    return;
                }

                // Chuyển tiếp response xuống client
                await response.WriteHeaderAsync(configuration.Client).ConfigureAwait(false);
                await response.WriteBodyAsync(configuration.Client).ConfigureAwait(false);
            }

            // Write connected response if needed
            else if (configuration.InitRequest.Method == HttpMethod.Connect)
            {
                await Helper.SendConnectedResponse(configuration.Proxy, configuration.Client).ConfigureAwait(false);
            }

            // Write init request header to remote if needed
            else
            {
                await configuration.InitRequest.WriteHeaderAsync(configuration.Remote).ConfigureAwait(false);
            }

            // Start transferring
            await Task.WhenAll([
                ClientToRemote(),
                RemoteToClient(),
            ]);
        }

        private async Task ClientToRemote()
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested) break;

                try
                {
                    var buffer = new Memory<byte>(new byte[4096]);
                    int bytesRead = await configuration.Client.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                    await configuration.Remote.WriteAsync(buffer[..bytesRead], cts.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    cts.Cancel();
                    break;
                }
            }
        }

        private async Task RemoteToClient()
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested) break;

                try
                {
                    var buffer = new Memory<byte>(new byte[4096]);
                    int bytesRead = await configuration.Remote.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                    await configuration.Client.WriteAsync(buffer[..bytesRead], cts.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    cts.Cancel();
                    break;
                }
            }
        }
    }
}