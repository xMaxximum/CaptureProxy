using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy.Tunnels
{
    internal class DecryptedTunnel
    {
        protected HttpRequest? _prevRequest;
        protected BeforeRequestEventArgs? _requestEvent;
        protected TunnelConfiguration _configuration;

        public DecryptedTunnel(TunnelConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task StartAsync()
        {
            await Task.WhenAll([
                ClientToRemote(),
                RemoteToClient(),
            ]);
        }

        protected virtual async Task ClientToRemote()
        {
            while (Settings.ProxyIsRunning)
            {
                // Read request header
                var request = _configuration.InitRequest;
                if (request == null)
                {
                    request = new HttpRequest();
                    await request.ReadHeaderAsync(_configuration.Client);
                }
                else
                {
                    _configuration.InitRequest = null;
                }

                // Store last request
                _prevRequest?.Dispose();
                _prevRequest = request;

                // Add proxy authorization header if needed
                if (
                    _configuration.TunnelEstablishEvent != null &&
                    _configuration.TunnelEstablishEvent.UpstreamProxy &&
                    _configuration.TunnelEstablishEvent.ProxyUser != null &&
                    _configuration.TunnelEstablishEvent.ProxyPass != null
                )
                {
                    request.Headers.SetProxyAuthorization(_configuration.TunnelEstablishEvent.ProxyUser, _configuration.TunnelEstablishEvent.ProxyPass);
                }

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

        protected async Task RemoteToClient()
        {
            while (Settings.ProxyIsRunning)
            {
                // Read response header
                using var response = new HttpResponse();
                await response.ReadHeaderAsync(_configuration.Remote);

                // If CaptureResponse disabled
                if (_requestEvent?.CaptureResponse != true)
                {
                    // Write header to client stream
                    await response.WriteHeaderAsync(_configuration.Client);

                    // Transfer body to client stream
                    if (response.Headers.ContentLength > 0)
                    {
                        int bytesRead = 0;
                        byte[] buffer = new byte[Settings.StreamBufferSize];

                        long remaining = response.Headers.ContentLength;
                        while (true)
                        {
                            if (remaining <= 0) break;
                            if (!Settings.ProxyIsRunning) break;

                            bytesRead = await _configuration.Remote.ReadAsync(buffer);
                            remaining -= bytesRead;

                            await _configuration.Client.Stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        }
                    }
                    else if (response.ChunkedTransfer)
                    {
                        while (Settings.ProxyIsRunning)
                        {
                            byte[] buffer = await response.ReadChunkAsync(_configuration.Remote);
                            await response.WriteChunkAsync(_configuration.Client, buffer);

                            if (buffer.Length == 0) break;
                        }
                    }

                    // Flush client stream
                    await _configuration.Client.Stream.FlushAsync();
                    continue;
                }

                // Handle event-stream response
                if (response.EventStream)
                {
                    bool headerWrited = false;
                    while (Settings.ProxyIsRunning)
                    {
                        // Read body from remote stream
                        await response.ReadEventStreamBody(_configuration.Remote);

                        // Before response event
                        if (_prevRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                        var e = new BeforeResponseEventArgs(_prevRequest, response);
                        Events.HandleBeforeResponse(this, e);

                        // Write to client stream
                        if (!headerWrited)
                        {
                            await response.WriteHeaderAsync(_configuration.Client);
                            headerWrited = true;
                        }

                        await response.WriteEventStreamBody(_configuration.Client);
                    }

                    break;
                }

                // Otherwise, normal response
                {
                    // Read body from remote stream
                    await response.ReadBodyAsync(_configuration.Remote);

                    // Before response event
                    if (_prevRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                    var e = new BeforeResponseEventArgs(_prevRequest, response);
                    Events.HandleBeforeResponse(this, e);

                    // Write to client stream
                    await response.WriteHeaderAsync(_configuration.Client);
                    await response.WriteBodyAsync(_configuration.Client);
                }
            }
        }
    }
}