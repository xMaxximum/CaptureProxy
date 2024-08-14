using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy.Tunnels
{
    internal class DecryptedTunnel(TunnelConfiguration configuration)
    {
        private HttpRequest? _prevRequest;
        private BeforeRequestEventArgs? _beforeRequestEvent;

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

                // Store last request
                _prevRequest?.Dispose();
                _prevRequest = request;

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

                // Read body from client stream
                await request.ReadBodyAsync(configuration.Client);

                // Before request event
                _beforeRequestEvent = new BeforeRequestEventArgs(request);
                Events.HandleBeforeRequest(this, _beforeRequestEvent);

                // Write custom respose if exists
                if (_beforeRequestEvent.Response != null)
                {
                    await _beforeRequestEvent.Response.WriteHeaderAsync(configuration.Client);
                    await _beforeRequestEvent.Response.WriteBodyAsync(configuration.Client);
                    continue;
                }

                // Write to remote stream
                await request.WriteHeaderAsync(configuration.Remote);
                await request.WriteBodyAsync(configuration.Remote);
            }
        }

        private async Task RemoteToClient()
        {
            try
            {
                while (Settings.ProxyIsRunning)
                {
                    // Read response header
                    using HttpResponse response = new HttpResponse();
                    await response.ReadHeaderAsync(configuration.Remote);

                    // If PacketCapture disabled
                    if (_tunnelEvent?.PacketCapture != true || _beforeRequestEvent?.CaptureResponse != true)
                    {
                        // Write header to client stream
                        await response.WriteHeaderAsync(client);

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

                                bytesRead = await Helper.StreamReadAsync(configuration.Remote.Stream, buffer, CancellationToken.None);
                                remaining -= bytesRead;

                                await client.Stream.WriteAsync(buffer, 0, bytesRead);
                            }
                        }
                        else if (response.ChunkedTransfer)
                        {
                            while (true)
                            {
                                if (!Settings.ProxyIsRunning) break;

                                byte[] buffer = await response.ReadChunkAsync(configuration.Remote);
                                await response.WriteChunkAsync(client, buffer);

                                if (buffer.Length == 0) break;
                            }
                        }

                        // Flush client stream
                        await client.Stream.FlushAsync();
                        continue;
                    }

                    // Handle event-stream response
                    if (response.EventStream)
                    {
                        bool headerWrited = false;
                        while (true)
                        {
                            if (!Settings.ProxyIsRunning) break;

                            // Read body from remote stream
                            await response.ReadEventStreamBody(remote);

                            // Before response event
                            if (_prevRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                            BeforeResponseEventArgs e = new BeforeResponseEventArgs(_prevRequest, response);
                            Events.HandleBeforeResponse(this, e);

                            // Write to client stream
                            if (!headerWrited)
                            {
                                await response.WriteHeaderAsync(client);
                                headerWrited = true;
                            }
                            await response.WriteEventStreamBody(client);
                        }

                        break;
                    }

                    // Otherwise, normal response
                    {
                        // Read body from remote stream
                        await response.ReadBodyAsync(remote);

                        // Before response event
                        if (_prevRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                        BeforeResponseEventArgs e = new BeforeResponseEventArgs(_prevRequest, response);
                        Events.HandleBeforeResponse(this, e);

                        // Write to client stream
                        await response.WriteHeaderAsync(client);
                        await response.WriteBodyAsync(client);
                    }
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }
        }
    }
}