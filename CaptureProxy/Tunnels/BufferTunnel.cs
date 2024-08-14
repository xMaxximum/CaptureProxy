using CaptureProxy.HttpIO;
using System;

namespace CaptureProxy.Tunnels
{
    internal class BufferTunnel(TunnelConfiguration configuration)
    {
        public async Task StartAsync()
        {
            await Task.WhenAll([
                ClientToRemote(),
                RemoteToClient(),
            ]);
        }

        private async Task ClientToRemote()
        {
            while (Settings.ProxyIsRunning)
            {
                // Read request header
                var request = configuration.InitRequest;
                if (request == null)
                {
                    request = new HttpRequest();
                    await request.ReadHeaderAsync(configuration.Client);
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
                await request.WriteHeaderAsync(configuration.Remote);

                if (request.Headers.ContentLength == 0) continue;

                int bytesRead = 0;
                long bytesRemaining = request.Headers.ContentLength;
                var buffer = new byte[4096];
                int bufferLength = 0;
                while (Settings.ProxyIsRunning)
                {
                    if (bytesRemaining <= 0) break;

                    bufferLength = (int)Math.Min(bytesRemaining, 4096);
                    bytesRead = await configuration.Client.ReadAsync(buffer.AsMemory(0, bufferLength));
                    await configuration.Remote.Stream.WriteAsync(buffer.AsMemory(0, bytesRead));

                    bytesRemaining -= bytesRead;
                }
            }
        }

        private async Task RemoteToClient()
        {
            int bytesRead = 0;
            var buffer = new Memory<byte>(new byte[4096]);
            while (Settings.ProxyIsRunning)
            {
                bytesRead = await configuration.Remote.ReadAsync(buffer);
                await configuration.Client.Stream.WriteAsync(buffer[..bytesRead]);
            }
        }
    }
}