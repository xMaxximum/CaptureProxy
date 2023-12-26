using CaptureProxy.MyEventArgs;
using System.Globalization;
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
        private EstablishRemoteEventArgs? _connectEvent;

        public DecryptedTunnel(Client client, Client remote, string baseUrl, EstablishRemoteEventArgs? connectEvent, CancellationToken token)
        {
            _client = client;
            _remote = remote;
            _token = token;
            _baseUrl = baseUrl;
            _connectEvent = connectEvent;
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

                    if (
                        request.RequestUri.Scheme == "http" &&
                        _connectEvent?.UpstreamProxy == true &&
                        _connectEvent?.ProxyUser != null &&
                        _connectEvent?.ProxyPass != null
                    )
                    {
                        request.Headers.SetProxyAuthorization(_connectEvent.ProxyUser, _connectEvent.ProxyPass);
                    }

                    await request.WriteHeaderAsync(_remote.Stream, _token).ConfigureAwait(false);

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

                    await _remote.Stream.FlushAsync(_token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex.Message);
                _running = false;
            }
        }

        private async Task RemoteToClient()
        {
            try
            {
                while (_running && !_token.IsCancellationRequested)
                {
                    HttpResponse response = new HttpResponse();
                    await response.ReadHeaderAsync(_remote.Stream, _token).ConfigureAwait(false);

                    await response.WriteHeaderAsync(_client.Stream, _token).ConfigureAwait(false);

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
                        Stream clientStream = _client.Stream;
                        Stream remoteStream = _remote.Stream;

                        while (_running && !_token.IsCancellationRequested)
                        {
                            string hexLength = await Helper.StreamReadLineAsync(_remote.Stream, Settings.MaxChunkSizeLine, _token).ConfigureAwait(false);
                            if (int.TryParse(hexLength, NumberStyles.HexNumber, null, out int chunkSize) == false)
                            {
                                throw new InvalidOperationException($"Chunk size {hexLength} is not valid.");
                            }

                            byte[] buffer = await Helper.StreamReadExactlyAsync(_remote.Stream, chunkSize, _token).ConfigureAwait(false);
                            string endOfChunk = await Helper.StreamReadLineAsync(_remote.Stream, 2, _token).ConfigureAwait(false);
                            if (string.IsNullOrEmpty(endOfChunk) == false)
                            {
                                throw new InvalidOperationException($"End of chunk {hexLength} is not CRLF bytes.");
                            }

                            await _client.Stream.WriteAsync(Encoding.UTF8.GetBytes(hexLength + "\r\n"), _token).ConfigureAwait(false);
                            await _client.Stream.WriteAsync(buffer, _token).ConfigureAwait(false);
                            await _client.Stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), _token).ConfigureAwait(false);

                            if (chunkSize == 0) break;

                            //string? contentEncoding = response.Headers.GetAsFisrtValue("Content-Encoding");
                            //if (contentEncoding != null)
                            //{
                            //    contentEncoding = contentEncoding.ToLower();
                            //    switch (contentEncoding)
                            //    {
                            //        case "gzip":

                            //            break;

                            //        default:
                            //            throw new NotSupportedException($"Content encoding {contentEncoding} is not supported.");
                            //    }
                            //}
                        }
                    }

                    await _client.Stream.FlushAsync(_token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Events.Log(ex.Message);
                _running = false;
            }
        }
    }
}