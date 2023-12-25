using CaptureProxy.MyEventArgs;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class HttpProxy : IDisposable
    {
        private int _port;
        private TcpListener _server;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        public HttpProxy(int port)
        {
            _port = port;
            _server = new TcpListener(IPAddress.Any, _port);
        }

        public void Start()
        {
            _server.Start();

            AcceptTcpClient().ConfigureAwait(false);

            Events.Log($"TcpServer started on port {_port}.");
        }

        private async Task AcceptTcpClient()
        {
            while (!_tokenSource.IsCancellationRequested)
            {
                TcpClient tcpClient = await _server.AcceptTcpClientAsync(_tokenSource.Token).ConfigureAwait(false);
                Client client = new Client(tcpClient);

                _ = Task.Run(() =>
                {
                    Session session = new Session(client, _tokenSource.Token);
                    Events.HandleSessionConnected(this, new SessionConnectedEventArgs(session));

                    try
                    {
                        // Don't know why if I not use Wait() here, the Exception has gone, no more throw
                        session.StartAsync().Wait();
                    }
                    catch (Exception ex)
                    {
                        Events.Log(ex.ToString());
                    }
                    finally
                    {
                        session.Stop();
                    }
                }).ConfigureAwait(false);
            }
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
            _tokenSource = new CancellationTokenSource();
            _server.Stop();

            Events.Log($"TcpServer stopped.");
        }

        public void Dispose()
        {
            Stop();

            _server.Dispose();
            _tokenSource.Dispose();
        }
    }
}
