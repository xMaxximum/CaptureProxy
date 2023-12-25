namespace CaptureProxy
{
    internal class BufferTunnel : Tunnel
    {
        private Client _client;
        private Client _remote;
        private CancellationToken _token;
        private byte[] _clientBuffer = new byte[Settings.StreamBufferSize];
        private byte[] _remoteBuffer = new byte[Settings.StreamBufferSize];
        private bool _running = false;

        public BufferTunnel(Client client, Client remote, CancellationToken token)
        {
            _client = client;
            _remote = remote;
            _token = token;
        }

        public override async Task StartAsync()
        {
            _running = true;

            if (RequestHeader != null)
            {
                await RequestHeader.WriteHeaderAsync(_remote.Stream, _token).ConfigureAwait(false);
            }

            ThreadPool.QueueUserWorkItem(ClientBeginRead);
            ThreadPool.QueueUserWorkItem(RemoteBeginRead);

            while (_running && !_token.IsCancellationRequested)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        public override void Stop()
        {
            _running = false;
        }

        private void ClientBeginRead(object? state)
        {
            if (!_running) return;
            if (_token.IsCancellationRequested) return;

            try
            {
                _client.Stream.BeginRead(_clientBuffer, 0, _clientBuffer.Length, ClientReadCallback, null);
            }
            catch (Exception ex)
            {
                //Events.Log(ex.Message);
                _running = false;
            }
        }

        private void ClientReadCallback(IAsyncResult ar)
        {
            if (!_running) return;
            if (_token.IsCancellationRequested) return;

            try
            {
                int bytesRead = _client.Stream.EndRead(ar);
                if (bytesRead == 0) throw new InvalidOperationException("Stream return no data.");
                _remote.Stream.Write(_clientBuffer, 0, bytesRead);
            }
            catch (Exception ex)
            {
                //Events.Log(ex.Message);
                _running = false;
                return;
            }

            ThreadPool.QueueUserWorkItem(ClientBeginRead);
        }

        private void RemoteBeginRead(object? state)
        {
            if (!_running) return;
            if (_token.IsCancellationRequested) return;

            try
            {
                _remote.Stream.BeginRead(_remoteBuffer, 0, _remoteBuffer.Length, RemoteReadCallback, null);
            }
            catch (Exception ex)
            {
                //Events.Log(ex.Message);
                _running = false;
            }
        }

        private void RemoteReadCallback(IAsyncResult ar)
        {
            if (!_running) return;
            if (_token.IsCancellationRequested) return;

            try
            {
                int bytesRead = _remote.Stream.EndRead(ar);
                if (bytesRead == 0) throw new InvalidOperationException("Stream return no data.");
                _client.Stream.Write(_remoteBuffer, 0, bytesRead);
            }
            catch (Exception ex)
            {
                //Events.Log(ex.Message);
                _running = false;
                return;
            }

            ThreadPool.QueueUserWorkItem(RemoteBeginRead);
        }
    }
}