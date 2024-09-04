using CaptureProxy.HttpIO;
using System;

namespace CaptureProxy.Tunnels
{
    internal class BufferTunnel(TunnelConfiguration configuration)
    {
        public async Task StartAsync()
        {
            //if (configuration.TunnelEstablishEvent.UpstreamProxy == true)
            //{
            //    using var connectRequest = new HttpRequest();
            //    connectRequest.Method = HttpMethod.Connect;
            //    connectRequest.Version = "HTTP/1.1";
            //    connectRequest.Uri = configuration.BaseUri;
            //    if (configuration.TunnelEstablishEvent.ProxyUser != null && configuration.TunnelEstablishEvent.ProxyPass != null)
            //    {
            //        connectRequest.Headers.SetProxyAuthorization(configuration.TunnelEstablishEvent.ProxyUser, configuration.TunnelEstablishEvent.ProxyPass);
            //    }
            //    await connectRequest.WriteHeaderAsync(remoteClient);
            //}

            // Upgrade to decrypted tunnel if needed
            bool useDecryptedTunnel = configuration.TunnelEstablishEvent.PacketCapture;

            if (
                configuration.TunnelEstablishEvent.PacketCapture == false && // Không sử dụng packet capture
                configuration.TunnelEstablishEvent.UpstreamProxy == true && // Có sử dụng upstream proxy
                configuration.TunnelEstablishEvent.ProxyUser != null && // Có sử dụng proxy basic authenticate
                configuration.TunnelEstablishEvent.ProxyPass != null && // Có sử dụng proxy basic authenticate
                configuration.InitRequest.Method != HttpMethod.Connect // Không sử dụng SSL
            )
            {
                useDecryptedTunnel = true;
            }

            if (useDecryptedTunnel)
            {
                var tunnel = new DecryptedTunnel(configuration);
                await tunnel.StartAsync();
                return;
            }

            // Write init request header to remote if needed
            if (configuration.InitRequest.Method != HttpMethod.Connect)
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