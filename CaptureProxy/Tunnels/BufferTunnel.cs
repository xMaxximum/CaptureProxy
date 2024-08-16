using CaptureProxy.HttpIO;
using System;

namespace CaptureProxy.Tunnels
{
    internal class BufferTunnel : BaseTunnel
    {
        public BufferTunnel(TunnelConfiguration configuration) : base(configuration) { }

        protected override async Task ClientToRemote()
        {
            if (configuration.UseSSL)
            {
                await ClientToRemoteWithHttps();
            }
            else
            {
                await ClientToRemoteWithHttp();
            }
        }

        private async Task ClientToRemoteWithHttp()
        {
            while (Settings.ProxyIsRunning)
            {
                // Read request header
                var request = configuration.InitRequest;
                if (request == null)
                {
                    request = new HttpRequest();
                    await request.ReadHeaderAsync(configuration.Client, configuration.BaseUri);
                }
                else
                {
                    configuration.InitRequest = null;
                }

                // Add proxy authorization header if needed
                if (
                    configuration.TunnelEstablishEvent != null &&
                    configuration.TunnelEstablishEvent.UpstreamProxy &&
                    configuration.TunnelEstablishEvent.ProxyUser != null &&
                    configuration.TunnelEstablishEvent.ProxyPass != null
                )
                {
                    request.Headers.SetProxyAuthorization(configuration.TunnelEstablishEvent.ProxyUser, configuration.TunnelEstablishEvent.ProxyPass);
                }

                // Write request header to remote
                await request.WriteHeaderAsync(configuration.Remote);

                // Check content length
                if (request.Headers.ContentLength == 0) continue;

                // Stream request body to remote
                long bytesRemaining = request.Headers.ContentLength;
                var buffer = new byte[4096];
                while (true)
                {
                    if (bytesRemaining <= 0) break;
                    if (!Settings.ProxyIsRunning) break;

                    int bufferLength = (int)Math.Min(bytesRemaining, 4096);
                    int bytesRead = await configuration.Client.ReadAsync(buffer.AsMemory(0, bufferLength));
                    await configuration.Remote.Stream.WriteAsync(buffer.AsMemory(0, bytesRead));

                    bytesRemaining -= bytesRead;
                }
            }
        }

        private async Task ClientToRemoteWithHttps()
        {
            var buffer = new Memory<byte>(new byte[4096]);
            while (Settings.ProxyIsRunning)
            {
                int bytesRead = await configuration.Client.ReadAsync(buffer);
                await configuration.Remote.Stream.WriteAsync(buffer[..bytesRead]);
            }
        }

        protected override async Task RemoteToClient()
        {
            var buffer = new Memory<byte>(new byte[4096]);
            while (Settings.ProxyIsRunning)
            {
                int bytesRead = await configuration.Remote.ReadAsync(buffer);
                await configuration.Client.Stream.WriteAsync(buffer[..bytesRead]);
            }
        }
    }
}