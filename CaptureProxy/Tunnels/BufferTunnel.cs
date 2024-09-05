using CaptureProxy.HttpIO;
using System;
using System.Net;

namespace CaptureProxy.Tunnels
{
    internal class BufferTunnel(TunnelConfiguration configuration)
    {
        public async Task StartAsync()
        {
            // Connect to upstream proxy if needed
            if (configuration.e.UpstreamProxy)
            {
                var request = configuration.InitRequest;
                await request.ReadBodyAsync(configuration.Client);

                // Sử dụng authentication có sẵn nếu được cung cấp
                if (configuration.e.ProxyUser != null && configuration.e.ProxyPass != null)
                {
                    request.Headers.SetProxyAuthorization(configuration.e.ProxyUser, configuration.e.ProxyPass);
                }

                // Chuyển tiếp request tới upstream proxy
                await request.WriteHeaderAsync(configuration.Remote);
                await request.WriteBodyAsync(configuration.Remote);

                // Đọc dữ liệu từ upstream proxy
                var response = new HttpResponse();
                await response.ReadHeaderAsync(configuration.Remote);
                await response.ReadBodyAsync(configuration.Remote);

                // Nếu proxy vẫn chưa xác thực thành công thì dừng lại
                if (response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    await Helper.SendBadGatewayResponse(configuration.Client);
                    return;
                }

                // Chuyển tiếp response xuống client
                await response.WriteHeaderAsync(configuration.Client);
                await response.WriteBodyAsync(configuration.Client);
            }

            // Write connected response if needed
            else if (configuration.InitRequest.Method == HttpMethod.Connect)
            {
                await Helper.SendConnectedResponse(configuration.Client);
            }

            // Write init request header to remote if needed
            else
            {
                await configuration.InitRequest.WriteHeaderAsync(configuration.Remote);
            }

            // Start transferring
            await Task.WhenAll([
                Task.Run(async () => {
                    while (Settings.ProxyIsRunning) await ClientToRemote();
                }),
                Task.Run(async () => {
                    while (Settings.ProxyIsRunning) await RemoteToClient();
                }),
            ]);
        }

        private async Task ClientToRemote()
        {
            var buffer = new Memory<byte>(new byte[4096]);
            int bytesRead = await configuration.Client.ReadAsync(buffer);
            await configuration.Remote.Stream.WriteAsync(buffer[..bytesRead]);
        }

        private async Task RemoteToClient()
        {
            var buffer = new Memory<byte>(new byte[4096]);
            int bytesRead = await configuration.Remote.ReadAsync(buffer);
            await configuration.Client.Stream.WriteAsync(buffer[..bytesRead]);
        }
    }
}