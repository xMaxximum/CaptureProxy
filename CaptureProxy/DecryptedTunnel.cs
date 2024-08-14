using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy
{
    internal class DecryptedTunnel : Tunnel
    {
        private Client _client;
        private Client _remote;
        private CancellationTokenSource _tokenSrc = new CancellationTokenSource();
        private string _baseUrl;
        private BeforeTunnelEstablishEventArgs? _tunnelEvent;
        private HttpRequest? _lastRequest;
        private BeforeRequestEventArgs? _beforeRequestEvent;

        public DecryptedTunnel(Client client, Client remote, string baseUrl, BeforeTunnelEstablishEventArgs? connectEvent)
        {
            _client = client;
            _remote = remote;
            _baseUrl = baseUrl;
            _tunnelEvent = connectEvent;
        }

        public override async Task StartAsync()
        {
            await Task.WhenAll([
                ClientToRemote(),
                RemoteToClient(),
            ]);
        }

        public override void Stop()
        {
            _tokenSrc.Cancel();
        }

        protected override bool ShouldStop()
        {
            return _tokenSrc.Token.IsCancellationRequested;
        }

        public override void Dispose()
        {
            Stop();

            _tokenSrc.Dispose();
        }

        private async Task ClientToRemote()
        {
            try
            {
                while (true)
                {
                    if (ShouldStop()) break;

                    // Read request header
                    HttpRequest request;
                    if (RequestHeader != null)
                    {
                        request = RequestHeader;
                        RequestHeader = null;
                    }
                    else
                    {
                        request = new HttpRequest();
                        await request.ReadHeaderAsync(_client._stream, _baseUrl, _tokenSrc.Token).ConfigureAwait(false);
                    }

                    // Store last request
                    _lastRequest?.Dispose();
                    _lastRequest = request;

                    // Add proxy authorization header if needed
                    if (
                        request.RequestUri.Scheme == "http" &&
                        _tunnelEvent?.UpstreamProxy == true &&
                        _tunnelEvent?.ProxyUser != null &&
                        _tunnelEvent?.ProxyPass != null
                    )
                    {
                        request.Headers.SetProxyAuthorization(_tunnelEvent.ProxyUser, _tunnelEvent.ProxyPass);
                    }

                    // If PacketCapture disabled
                    if (_tunnelEvent?.PacketCapture != true)
                    {
                        // Write header to remote stream
                        await request.WriteHeaderAsync(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);

                        // Transfer body to remote stream
                        if (request.Headers.ContentLength > 0)
                        {
                            int bytesRead = 0;
                            byte[] buffer = new byte[Settings.StreamBufferSize];

                            long remaining = request.Headers.ContentLength.Value;
                            while (true)
                            {
                                if (remaining <= 0) break;
                                if (ShouldStop()) break;

                                bytesRead = await Helper.StreamReadAsync(_client._stream, buffer, _tokenSrc.Token).ConfigureAwait(false);
                                remaining -= bytesRead;

                                await _remote._stream.WriteAsync(buffer, 0, bytesRead, _tokenSrc.Token).ConfigureAwait(false);
                            }
                        }

                        // Flush remote stream
                        await _remote._stream.FlushAsync(_tokenSrc.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Read body from client stream
                    await request.ReadBodyAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);

                    // Before request event
                    _beforeRequestEvent = new BeforeRequestEventArgs(request);
                    Events.HandleBeforeRequest(this, _beforeRequestEvent);

                    // Write custom respose if exists
                    if (_beforeRequestEvent.Response != null)
                    {
                        await _beforeRequestEvent.Response.WriteHeaderAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);
                        await _beforeRequestEvent.Response.WriteBodyAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Write to remote stream
                    await request.WriteHeaderAsync(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);
                    await request.WriteBodyAsync(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }
            finally
            {
                Stop();
                _lastRequest?.Dispose();
            }
        }

        private async Task RemoteToClient()
        {
            try
            {
                while (true)
                {
                    if (ShouldStop()) break;

                    // Read response header
                    using HttpResponse response = new HttpResponse();
                    await response.ReadHeaderAsync(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);

                    // If PacketCapture disabled
                    if (_tunnelEvent?.PacketCapture != true || _beforeRequestEvent?.CaptureResponse != true)
                    {
                        // Write header to client stream
                        await response.WriteHeaderAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);

                        // Transfer body to client stream
                        if (response.Headers.ContentLength > 0)
                        {
                            int bytesRead = 0;
                            byte[] buffer = new byte[Settings.StreamBufferSize];

                            long remaining = response.Headers.ContentLength.Value;
                            while (true)
                            {
                                if (remaining <= 0) break;
                                if (ShouldStop()) break;

                                bytesRead = await Helper.StreamReadAsync(_remote._stream, buffer, _tokenSrc.Token).ConfigureAwait(false);
                                remaining -= bytesRead;

                                await _client._stream.WriteAsync(buffer, 0, bytesRead, _tokenSrc.Token).ConfigureAwait(false);
                            }
                        }
                        else if (response.ChunkedTransfer)
                        {
                            while (true)
                            {
                                if (ShouldStop()) break;

                                byte[] buffer = await response.ReadChunkAsync(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);
                                await response.WriteChunkAsync(_client._stream, buffer, _tokenSrc.Token).ConfigureAwait(false);

                                if (buffer.Length == 0) break;
                            }
                        }

                        // Flush client stream
                        await _client._stream.FlushAsync(_tokenSrc.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Handle event-stream response
                    if (response.EventStream)
                    {
                        bool headerWrited = false;
                        while (true)
                        {
                            if (ShouldStop()) break;

                            // Read body from remote stream
                            await response.ReadEventStreamBody(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);

                            // Before response event
                            if (_lastRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                            BeforeResponseEventArgs e = new BeforeResponseEventArgs(_lastRequest, response);
                            Events.HandleBeforeResponse(this, e);

                            // Write to client stream
                            if (!headerWrited)
                            {
                                await response.WriteHeaderAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);
                                headerWrited = true;
                            }
                            await response.WriteEventStreamBody(_client._stream, _tokenSrc.Token).ConfigureAwait(false);
                        }

                        break;
                    }

                    // Otherwise, normal response
                    {
                        // Read body from remote stream
                        await response.ReadBodyAsync(_remote._stream, _tokenSrc.Token).ConfigureAwait(false);

                        // Before response event
                        if (_lastRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                        BeforeResponseEventArgs e = new BeforeResponseEventArgs(_lastRequest, response);
                        Events.HandleBeforeResponse(this, e);

                        // Write to client stream
                        await response.WriteHeaderAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);
                        await response.WriteBodyAsync(_client._stream, _tokenSrc.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }
            finally
            {
                Stop();
            }
        }
    }
}