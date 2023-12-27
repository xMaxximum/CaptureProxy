using CaptureProxy.MyEventArgs;
using System.Globalization;
using System.IO;
using System.Text;

namespace CaptureProxy
{
    internal class DecryptedTunnel : Tunnel
    {
        private Client _client;
        private Client _remote;
        private CancellationToken _token;
        private bool _running = false;
        private string _baseUrl;
        private BeforeTunnelConnectEventArgs? _tunnelEvent;
        private HttpRequest? lastRequest;
        private BeforeRequestEventArgs? _beforeRequestEvent;

        public DecryptedTunnel(Client client, Client remote, string baseUrl, BeforeTunnelConnectEventArgs? connectEvent, CancellationToken token)
        {
            _client = client;
            _remote = remote;
            _token = token;
            _baseUrl = baseUrl;
            _tunnelEvent = connectEvent;
        }

        public override async Task StartAsync()
        {
            _running = true;

            _ = Task.Run(ClientToRemote).ConfigureAwait(false);
            _ = Task.Run(RemoteToClient).ConfigureAwait(false);

            while (_running && !_token.IsCancellationRequested)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        public override void Stop()
        {
            _running = false;
        }

        private async Task ClientToRemote()
        {
            if (!_running) return;
            if (_token.IsCancellationRequested) return;

            try
            {
                while (_running && !_token.IsCancellationRequested)
                {
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
                        await request.ReadHeaderAsync(_client.Stream, _baseUrl, _token).ConfigureAwait(false);
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
                        await request.WriteHeaderAsync(_remote.Stream, _token).ConfigureAwait(false);

                        // Transfer body to remote stream
                        if (request.Headers.ContentLength > 0)
                        {
                            long remaining = request.Headers.ContentLength.Value;
                            while (_running && !_token.IsCancellationRequested && remaining > 0)
                            {
                                byte[] buffer = await Helper.StreamReadAsync(_client.Stream, Math.Min(remaining, Settings.StreamBufferSize), _token).ConfigureAwait(false);
                                remaining -= buffer.Length;

                                await _remote.Stream.WriteAsync(buffer, _token).ConfigureAwait(false);
                            }
                        }

                        // Flush remote stream
                        await _remote.Stream.FlushAsync(_token).ConfigureAwait(false);
                        continue;
                    }

                    // Read body from client stream
                    await request.ReadBodyAsync(_client.Stream, _token).ConfigureAwait(false);

                    // Before request event
                    _beforeRequestEvent = new BeforeRequestEventArgs(request);
                    Events.HandleBeforeRequest(this, _beforeRequestEvent);

                    // Write custom respose if exists
                    if (_beforeRequestEvent.Response != null)
                    {
                        await _beforeRequestEvent.Response.WriteHeaderAsync(_client.Stream, _token).ConfigureAwait(false);
                        await _beforeRequestEvent.Response.WriteBodyAsync(_client.Stream, _token).ConfigureAwait(false);
                        continue;
                    }

                    // Write to remote stream
                    await request.WriteHeaderAsync(_remote.Stream, _token).ConfigureAwait(false);
                    await request.WriteBodyAsync(_remote.Stream, _token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex.ToString());
            }
            finally
            {
                _running = false;
                lastRequest?.Dispose();
            }
        }

        private async Task RemoteToClient()
        {
            try
            {
                while (_running && !_token.IsCancellationRequested)
                {
                    // Read response header
                    using HttpResponse response = new HttpResponse();
                    await response.ReadHeaderAsync(_remote.Stream, _token).ConfigureAwait(false);

                    // If PacketCapture disabled
                    if (_tunnelEvent?.PacketCapture != true || _beforeRequestEvent?.CaptureResponse != true)
                    {
                        // Write header to client stream
                        await response.WriteHeaderAsync(_client.Stream, _token).ConfigureAwait(false);

                        // Transfer body to client stream
                        if (response.Headers.ContentLength > 0)
                        {
                            long remaining = response.Headers.ContentLength.Value;
                            while (_running && !_token.IsCancellationRequested && remaining > 0)
                            {
                                byte[] buffer = await Helper.StreamReadAsync(_remote.Stream, Math.Min(remaining, Settings.StreamBufferSize), _token).ConfigureAwait(false);
                                remaining -= buffer.Length;

                                await _client.Stream.WriteAsync(buffer, _token).ConfigureAwait(false);
                            }
                        }
                        else if (response.ChunkedTransfer)
                        {
                            while (_running && !_token.IsCancellationRequested)
                            {
                                byte[] buffer = await response.ReadChunkAsync(_remote.Stream, _token).ConfigureAwait(false);

                                string hexLength = buffer.Length.ToString("X").ToLower();
                                await _client.Stream.WriteAsync(Encoding.UTF8.GetBytes(hexLength + "\r\n"), _token).ConfigureAwait(false);
                                await _client.Stream.WriteAsync(buffer, _token).ConfigureAwait(false);
                                await _client.Stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), _token).ConfigureAwait(false);

                                if (buffer.Length == 0) break;
                            }
                        }

                        // Flush client stream
                        await _client.Stream.FlushAsync(_token).ConfigureAwait(false);
                        continue;
                    }

                    // Read body from remote stream
                    await response.ReadBodyAsync(_remote.Stream, _token).ConfigureAwait(false);

                    // Before response event
                    if (lastRequest == null) throw new InvalidOperationException("Response without request, huh?!!");
                    BeforeResponseEventArgs e = new BeforeResponseEventArgs(lastRequest, response);
                    Events.HandleBeforeResponse(this, e);

                    // Write to client stream
                    await response.WriteHeaderAsync(_client.Stream, _token).ConfigureAwait(false);
                    await response.WriteBodyAsync(_client.Stream, _token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex.ToString());
            }
            finally
            {
                _running = false;
            }
        }
    }
}