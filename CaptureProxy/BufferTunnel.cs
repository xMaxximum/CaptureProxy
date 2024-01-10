namespace CaptureProxy
{
    internal class BufferTunnel : Tunnel
    {
        private Client _client;
        private Client _remote;
        private CancellationTokenSource _tokenSrc = new CancellationTokenSource();

        public BufferTunnel(Client client, Client remote)
        {
            _client = client;
            _remote = remote;
        }

        public override async Task StartAsync()
        {
            if (RequestHeader != null)
            {
                await RequestHeader.WriteHeaderAsync(_remote.Stream, _tokenSrc.Token).ConfigureAwait(false);
            }

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
                int bytesRead = 0;
                byte[] buffer = new byte[Settings.StreamBufferSize];

                while (true)
                {
                    if (ShouldStop()) break;

                    bytesRead = await Helper.StreamReadAsync(_client.Stream, buffer, _tokenSrc.Token).ConfigureAwait(false);
                    await _remote.Stream.WriteAsync(buffer, 0, bytesRead, _tokenSrc.Token).ConfigureAwait(false);
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

        private async Task RemoteToClient()
        {
            try
            {
                int bytesRead = 0;
                byte[] buffer = new byte[Settings.StreamBufferSize];

                while (true)
                {
                    if (ShouldStop()) break;

                    bytesRead = await Helper.StreamReadAsync(_remote.Stream, buffer, _tokenSrc.Token).ConfigureAwait(false);
                    await _client.Stream.WriteAsync(buffer, 0, bytesRead, _tokenSrc.Token).ConfigureAwait(false);
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