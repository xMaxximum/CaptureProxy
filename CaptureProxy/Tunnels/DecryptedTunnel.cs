using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy.Tunnels
{
    internal class DecryptedTunnel : BaseTunnel
    {
        protected HttpRequest? _prevRequest;

        public DecryptedTunnel(TunnelConfiguration configuration) : base(configuration) { }

        protected override async Task ClientToRemote()
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

                // Read body from client stream
                await request.ReadBodyAsync(configuration.Client);

                // Store last request
                _prevRequest?.Dispose();
                _prevRequest = Helper.DeepClone(request);

                // Before request event
                var e = new BeforeRequestEventArgs(request);
                Events.HandleBeforeRequest(this, e);

                // Add proxy authorization header if needed
                if (
                    !configuration.UseSSL &&
                    configuration.TunnelEstablishEvent != null &&
                    configuration.TunnelEstablishEvent.UpstreamProxy &&
                    configuration.TunnelEstablishEvent.ProxyUser != null &&
                    configuration.TunnelEstablishEvent.ProxyPass != null
                )
                {
                    request.Headers.SetProxyAuthorization(configuration.TunnelEstablishEvent.ProxyUser, configuration.TunnelEstablishEvent.ProxyPass);
                }

                // Update host header with request uri host
                request.Headers.AddOrReplace("host", request.Uri.Authority);

                // Write custom respose if exists
                if (e.Response != null)
                {
                    await e.Response.WriteHeaderAsync(configuration.Client);
                    await e.Response.WriteBodyAsync(configuration.Client);
                    continue;
                }

                // Write to remote stream
                await request.WriteHeaderAsync(configuration.Remote);
                await request.WriteBodyAsync(configuration.Remote);
            }
        }

        protected override async Task RemoteToClient()
        {
            while (Settings.ProxyIsRunning)
            {
                if (_prevRequest == null) continue;

                // Read response header
                using var response = new HttpResponse();
                await response.ReadHeaderAsync(configuration.Remote);

                // Trigger before header response event
                var beforeHeaderEvent = new BeforeHeaderResponseEventArgs(_prevRequest, response);
                Events.HandleBeforeHeaderResponse(this, beforeHeaderEvent);

                // If CaptureBody disabled
                if (!beforeHeaderEvent.CaptureBody)
                {
                    // Write header to client stream
                    await response.WriteHeaderAsync(configuration.Client);

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

                            bytesRead = await configuration.Remote.ReadAsync(buffer);
                            remaining -= bytesRead;

                            await configuration.Client.Stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        }
                    }
                    else if (response.ChunkedTransfer)
                    {
                        while (Settings.ProxyIsRunning)
                        {
                            byte[] buffer = await response.ReadChunkAsync(configuration.Remote);
                            await response.WriteChunkAsync(configuration.Client, buffer);

                            if (buffer.Length == 0) break;
                        }
                    }

                    // Flush client stream
                    await configuration.Client.Stream.FlushAsync();
                    continue;
                }

                // Handle event-stream response
                if (response.EventStream)
                {
                    bool headerSent = false;
                    while (Settings.ProxyIsRunning)
                    {
                        // Read body from remote stream
                        await response.ReadEventStreamBody(configuration.Remote);

                        // Trigger before body response event
                        var beforeBodyEvent = new BeforeBodyResponseEventArgs(_prevRequest, response);
                        Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                        // Write header to client stream
                        if (!headerSent)
                        {
                            // Send header here because content-length may be changed
                            // after trigger before body response event
                            await response.WriteHeaderAsync(configuration.Client);
                            headerSent = true;
                        }

                        // Write body to client stream
                        await response.WriteEventStreamBody(configuration.Client);
                    }

                    break;
                }

                // Otherwise, normal response
                {
                    // Read body from remote stream
                    await response.ReadBodyAsync(configuration.Remote);

                    // Trigger before body response event
                    var beforeBodyEvent = new BeforeBodyResponseEventArgs(_prevRequest, response);
                    Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                    // Write to client stream
                    await response.WriteHeaderAsync(configuration.Client);
                    await response.WriteBodyAsync(configuration.Client);
                }
            }
        }
    }
}