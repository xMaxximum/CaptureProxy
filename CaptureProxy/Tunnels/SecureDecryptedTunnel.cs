using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy.Tunnels
{
    internal class SecureDecryptedTunnel : DecryptedTunnel
    {
        public SecureDecryptedTunnel(TunnelConfiguration _configuration) : base(_configuration) { }

        protected override async Task ClientToRemote()
        {
            while (Settings.ProxyIsRunning)
            {
                // Read request header
                var request = new HttpRequest();
                await request.ReadHeaderAsync(_configuration.Client);

                // Store last request
                _prevRequest?.Dispose();
                _prevRequest = request;

                // Read body from client stream
                await request.ReadBodyAsync(_configuration.Client);

                // Before request event
                _requestEvent = new BeforeRequestEventArgs(request);
                Events.HandleBeforeRequest(this, _requestEvent);

                // Write custom respose if exists
                if (_requestEvent.Response != null)
                {
                    await _requestEvent.Response.WriteHeaderAsync(_configuration.Client);
                    await _requestEvent.Response.WriteBodyAsync(_configuration.Client);
                    continue;
                }

                // Write to remote stream
                await request.WriteHeaderAsync(_configuration.Remote);
                await request.WriteBodyAsync(_configuration.Remote);
            }
        }
    }
}