using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy.Tunnels
{
    internal class DecryptedTunnel(TunnelConfiguration configuration)
    {
        private bool initRequestProcessed = false;

        public async Task StartAsync()
        {
            // Upgrade to ssl stream if needed
            if (configuration.InitRequest.Method == HttpMethod.Connect)
            {
                configuration.Client.AuthenticateAsServer(configuration.BaseUri.Host);
                configuration.Remote.AuthenticateAsClient(configuration.BaseUri.Host);
                initRequestProcessed = true;
            }

            // Start transferring
            while (Settings.ProxyIsRunning)
            {
                //await Task.Delay(10);

                using var request = await ClientToRemote();
                if (request == null) continue;

                await RemoteToClient(request);
            }
        }

        private async Task<HttpRequest?> ClientToRemote()
        {
            // Read request header
            var request = configuration.InitRequest;
            if (initRequestProcessed)
            {
                request = new HttpRequest();
                await request.ReadHeaderAsync(configuration.Client, configuration.BaseUri);
            }

            // Read body from client stream
            await request.ReadBodyAsync(configuration.Client);

            // Store original request
            var originRequest = Helper.DeepClone(request);

            // Before request event
            var e = new BeforeRequestEventArgs(request);
            Events.HandleBeforeRequest(this, e);

            // Update host header with request uri host
            request.Headers.AddOrReplace("host", request.Uri.Authority);

            // Write custom respose if exists
            if (e.Response != null)
            {
                await e.Response.WriteHeaderAsync(configuration.Client);
                await e.Response.WriteBodyAsync(configuration.Client);
                return null;
            }

            // Write to remote stream
            await request.WriteHeaderAsync(configuration.Remote);
            await request.WriteBodyAsync(configuration.Remote);

            // Init request processed
            initRequestProcessed = true;

            // Return original request
            return originRequest;
        }

        private async Task RemoteToClient(HttpRequest request)
        {
            // Read response header
            using var response = new HttpResponse();
            await response.ReadHeaderAsync(configuration.Remote);

            // Trigger before header response event
            var beforeHeaderEvent = new BeforeHeaderResponseEventArgs(request, response);
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
                return;
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
                    var beforeBodyEvent = new BeforeBodyResponseEventArgs(request, response);
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

                return;
            }

            // Otherwise, normal response
            {
                // Read body from remote stream
                await response.ReadBodyAsync(configuration.Remote);

                // Trigger before body response event
                var beforeBodyEvent = new BeforeBodyResponseEventArgs(request, response);
                Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                // Write to client stream
                await response.WriteHeaderAsync(configuration.Client);
                await response.WriteBodyAsync(configuration.Client);
            }
        }
    }
}