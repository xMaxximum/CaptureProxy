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
        private BeforeTunnelConnectEventArgs? _tunnelEvent;
        private HttpRequest? lastRequest;
        private BeforeRequestEventArgs? _beforeRequestEvent;

        public DecryptedTunnel(Client client, Client remote, string baseUrl, BeforeTunnelConnectEventArgs? connectEvent)
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
                        await request.ReadHeaderAsync(_client.Stream, _baseUrl, _tokenSrc.Token).ConfigureAwait(false);
                    }

                    // Store last request
                    lastRequest?.Dispose();
                    lastRequest = request;

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
                        await request.WriteHeaderAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);

                        // Transfer body to remote stream
                        if (request.Headers.ContentLength > 0)
                        {
                            long remaining = request.Headers.ContentLength.Value;
                            while (true)
                            {
                                if (remaining <= 0) break;
                                if (ShouldStop()) break;

                                byte[] buffer = await Helper.StreamReadAsync(_client.Stream, Math.Min(remaining, Settings.StreamBufferSize), _tokenSrc.Token).ConfigureAwait(false);
                                remaining -= buffer.Length;

                                await _remote.Stream.WriteAsync(buffer, _tokenSrc.Token).ConfigureAwait(false);
                            }
                        }

                        // Flush remote stream
                        await _remote.Stream.FlushAsync(_tokenSrc.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Read body from client stream
                    await request.ReadBodyAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);

                    // Before request event
                    _beforeRequestEvent = new BeforeRequestEventArgs(request);
                    Events.HandleBeforeRequest(this, _beforeRequestEvent);

                    // Write custom respose if exists
                    if (_beforeRequestEvent.Response != null)
                    {
                        await _beforeRequestEvent.Response.WriteHeaderAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
                        await _beforeRequestEvent.Response.WriteBodyAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Write to remote stream
                    await request.WriteHeaderAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);
                    await request.WriteBodyAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }
            finally
            {
                Stop();
                lastRequest?.Dispose();
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
                    await response.ReadHeaderAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);

                    // If PacketCapture disabled
                    if (_tunnelEvent?.PacketCapture != true || _beforeRequestEvent?.CaptureResponse != true)
                    {
                        // Write header to client stream
                        await response.WriteHeaderAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);

                        // Transfer body to client stream
                        if (response.Headers.ContentLength > 0)
                        {
                            long remaining = response.Headers.ContentLength.Value;
                            while (true)
                            {
                                if (remaining <= 0) break;
                                if (ShouldStop()) break;

                                byte[] buffer = await Helper.StreamReadAsync(_remote.Stream, Math.Min(remaining, Settings.StreamBufferSize), _tokenSrc.Token).ConfigureAwait(false);
                                remaining -= buffer.Length;

                                await _client.Stream.WriteAsync(buffer, _tokenSrc.Token).ConfigureAwait(false);
                            }
                        }
                        else if (response.ChunkedTransfer)
                        {
                            while (true)
                            {
                                if (ShouldStop()) break;

                                byte[] buffer = await response.ReadChunkAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);

                                string hexLength = buffer.Length.ToString("X").ToLower();
                                await _client.Stream.WriteAsync(Encoding.UTF8.GetBytes(hexLength + "\r\n"), _tokenSrc.Token).ConfigureAwait(false);
                                await _client.Stream.WriteAsync(buffer, _tokenSrc.Token).ConfigureAwait(false);
                                await _client.Stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), _tokenSrc.Token).ConfigureAwait(false);

                                if (buffer.Length == 0) break;
                            }
                        }

                        // Flush client stream
                        await _client.Stream.FlushAsync(_tokenSrc.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Read body from remote stream
                    await response.ReadBodyAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);

                    // Before response event
                    if (lastRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                    BeforeResponseEventArgs e = new BeforeResponseEventArgs(lastRequest, response);
                    Events.HandleBeforeResponse(this, e);

                    // Write to client stream
                    await response.WriteHeaderAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
                    await response.WriteBodyAsync(_client.Stream, _tokenSrc.Token).ConfigureAwait(false);
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