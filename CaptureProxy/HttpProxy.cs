using CaptureProxy.MyEventArgs;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class HttpProxy : IDisposable
    {
        private readonly int _port;
        private readonly TcpListener _server;

        public HttpProxy(int port)
        {
            _port = port;
            _server = new TcpListener(IPAddress.Any, _port);
        }

        public void Start()
        {
            _server.Start();

            Settings.ProxyIsRunning = true;
            Task.Run(AcceptTcpClient);

#if DEBUG
            Events.Log($"TcpServer started on port {_port}.");
#endif
        }

        private async Task AcceptTcpClient()
        {
            while (Settings.ProxyIsRunning)
            {
                var tcpClient = await _server.AcceptTcpClientAsync();
                var client = new Client(tcpClient);

                _ = SessionHandle(client);
            }
        }

        private async Task SessionHandle(Client client)
        {
            var session = new Session(client);

            try
            {
                Events.HandleSessionConnected(this, new SessionConnectedEventArgs(session));
                await session.StartAsync();
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
        }

        public void Stop()
        {
            Settings.ProxyIsRunning = false;
            _server.Stop();

#if DEBUG
            Events.Log($"TcpServer stopped.");
#endif
        }

        public void Dispose()
        {
            Stop();
            _server.Dispose();
        }
    }
}
