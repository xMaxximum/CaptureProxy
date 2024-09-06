using CaptureProxy.MyEventArgs;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class HttpProxy : IDisposable
    {
        public Events Events { get; } = new Events();
        public int SessionCount { get => sessions.Count; }

        internal Settings Settings { get; private set; }
        internal CancellationToken Token { get => cts.Token; }

        private readonly int port;
        private readonly TcpListener server;
        private readonly List<Session> sessions = [];
        private CancellationTokenSource cts = new();

        public HttpProxy(int port, Settings? settings = null)
        {
            this.port = port;
            this.server = new TcpListener(IPAddress.Any, this.port);

            this.Settings = settings ?? new Settings();
        }

        public void Start()
        {
            cts.Cancel();
            cts.Dispose();
            cts = new CancellationTokenSource();

            server.Start();

            Task.Run(AcceptTcpClient);

#if DEBUG
            Events.Log($"TcpServer started on port {port}.");
#endif
        }

        private async Task AcceptTcpClient()
        {
            while (true)
            {
                if (Token.IsCancellationRequested) break;

                var tcpClient = await server.AcceptTcpClientAsync(Token).ConfigureAwait(false);

                _ = Task.Run(async () =>
                {
                    var client = new Client(this, tcpClient);
                    await SessionHandle(client);
                }).ConfigureAwait(false);
            }
        }

        private async Task SessionHandle(Client client)
        {
            var session = new Session(this, client);
            sessions.Add(session);

            try
            {
                Events.HandleSessionConnected(this, new SessionConnectedEventArgs(session));
                await session.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Events.Log(ex);
            }

            session.Dispose();
            sessions.Remove(session);
        }

        public async Task StopAsync()
        {
            cts.Cancel();
            server.Stop();

            while (SessionCount > 0)
            {
                await Task.Delay(1000);
            }
#if DEBUG
            Events.Log($"TcpServer stopped.");
#endif
        }

        public void Dispose()
        {
            StopAsync().Wait();
            server.Dispose();
            cts.Dispose();
        }
    }
}
