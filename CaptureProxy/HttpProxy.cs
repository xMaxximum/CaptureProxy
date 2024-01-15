using CaptureProxy.MyEventArgs;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class HttpProxy : IDisposable
    {
        private int _port;
        private TcpListener _server;
        private CancellationTokenSource _tokenSrc = new CancellationTokenSource();

        private int _sessionCount = 0;
        public int SessionCount => _sessionCount;

        public HttpProxy(int port)
        {
            _port = port;
            _server = new TcpListener(IPAddress.Any, _port);

            Events.SessionConnected += (s, e) => Interlocked.Increment(ref _sessionCount);
            Events.SessionDisconnected += (s, e) => Interlocked.Decrement(ref _sessionCount);
        }

        public void Start()
        {
            _server.Start();

            AcceptTcpClient().ConfigureAwait(false);

#if DEBUG
            Events.Log($"TcpServer started on port {_port}.");
#endif
        }

        private async Task AcceptTcpClient()
        {
            while (!_tokenSrc.IsCancellationRequested)
            {
                TcpClient tcpClient = await _server.AcceptTcpClientAsync(_tokenSrc.Token).ConfigureAwait(false);
                Client client = new Client(tcpClient);

                _ = Task.Run(async () =>
                {
                    Session session = new Session(client, _tokenSrc.Token);

                    try
                    {
                        Events.HandleSessionConnected(this, new SessionConnectedEventArgs(session));
                        await session.StartAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Events.Log(ex);
                    }
                    finally
                    {
                        session.Stop();
                        session.Dispose();
                    }
                }).ConfigureAwait(false);
            }
        }

        public void Stop()
        {
            _tokenSrc.Cancel();

            _tokenSrc.Dispose();
            _tokenSrc = new CancellationTokenSource();

            _server.Stop();

#if DEBUG
            Events.Log($"TcpServer stopped.");
#endif
        }

        public void Dispose()
        {
            Stop();

            _server.Dispose();
            _tokenSrc.Dispose();
        }
    }
}
