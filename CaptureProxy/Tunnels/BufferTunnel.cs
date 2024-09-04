using CaptureProxy.HttpIO;
using System;
using System.Net;

namespace CaptureProxy.Tunnels
{
    internal class BufferTunnel(TunnelConfiguration configuration)
    {
        private async Task<HttpResponse> GetUpstreamProxyResponse(HttpRequest request)
        {
            await request.WriteHeaderAsync(configuration.Remote);
            await request.WriteBodyAsync(configuration.Remote);

            var response = new HttpResponse();
            await response.ReadHeaderAsync(configuration.Remote);
            await response.ReadBodyAsync(configuration.Remote);

            return response;
        }

        public async Task StartAsync()
        {
            // Upgrade to decrypted tunnel if needed
            bool useDecryptedTunnel = configuration.TunnelEstablishEvent.PacketCapture;

            //if (
            //    configuration.TunnelEstablishEvent.PacketCapture == false && // Không sử dụng packet capture
            //    configuration.TunnelEstablishEvent.UpstreamProxy == true && // Có sử dụng upstream proxy
            //    configuration.TunnelEstablishEvent.ProxyUser != null && // Có sử dụng proxy basic authenticate
            //    configuration.TunnelEstablishEvent.ProxyPass != null && // Có sử dụng proxy basic authenticate
            //    configuration.InitRequest.Method != HttpMethod.Connect // Không sử dụng SSL
            //)
            //{
            //    useDecryptedTunnel = true;
            //}

            if (useDecryptedTunnel)
            {
                var tunnel = new DecryptedTunnel(configuration);
                await tunnel.StartAsync();
                return;
            }

            // Connect to upstream proxy if needed
            if (configuration.TunnelEstablishEvent.UpstreamProxy)
            {
                var request = configuration.InitRequest;
                await request.ReadBodyAsync(configuration.Client);

                // Chuyển tiếp request tới upstream proxy
                var response = await GetUpstreamProxyResponse(request);

                // Nếu proxy yêu cầu authenticate, sử dụng authentication do user cung cấp trong event và thử lại
                if (response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired && configuration.TunnelEstablishEvent.ProxyUser != null && configuration.TunnelEstablishEvent.ProxyPass != null)
                {
                    request.Headers.SetProxyAuthorization(configuration.TunnelEstablishEvent.ProxyUser, configuration.TunnelEstablishEvent.ProxyPass);

                    response.Dispose();
                    response = await GetUpstreamProxyResponse(request);
                }

                // Nếu proxy yêu cầu authenticate, chuyển tiếp response xuống client và đợi action từ user
                while (response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    request.Dispose();
                    request = new HttpRequest();
                    await request.ReadHeaderAsync(configuration.Client);
                    await request.ReadBodyAsync(configuration.Client);

                    response.Dispose();
                    response = await GetUpstreamProxyResponse(request);
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