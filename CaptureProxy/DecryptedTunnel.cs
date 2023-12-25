


namespace CaptureProxy
{
    internal class DecryptedTunnel : Tunnel
    {
        private Client _client;
        private Client _remote;
        private CancellationToken _token;
        private bool _running = false;
        private string _baseUrl;

        public DecryptedTunnel(Client client, Client remote, string baseUrl, CancellationToken token)
        {
            _client = client;
            _remote = remote;
            _token = token;
            _baseUrl = baseUrl;
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