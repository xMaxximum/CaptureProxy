using CaptureProxy.HttpIO;

namespace CaptureProxy.Tunnels
{
    internal class SecureBufferTunnel(TunnelConfiguration configuration)
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
            var buffer = new Memory<byte>(new byte[4096]);
            while (Settings.ProxyIsRunning)
            {
                int bytesRead = await configuration.Client.ReadAsync(buffer);
                await configuration.Remote.Stream.WriteAsync(buffer[..bytesRead]);
            }
        }

        private async Task RemoteToClient()
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