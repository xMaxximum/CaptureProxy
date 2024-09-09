using CaptureProxy.MyEventArgs;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class HttpProxy : IDisposable
    {
        public Events Events { get; } = new Events();

        private int sessionCount = 0;
        public int SessionCount { get => sessionCount; }

        internal Settings Settings { get; private set; }
        internal CancellationToken Token { get => cts.Token; }

        private readonly int port;
        private readonly TcpListener server;
        private CancellationTokenSource cts = new();
        private bool isStopped = true;
        private bool isDisposed = false;

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

            isStopped = false;
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
            Interlocked.Increment(ref sessionCount);

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
            Interlocked.Decrement(ref sessionCount);
        }

        public void Stop()
        {
            if (isStopped) return;

            cts.Cancel();
            server.Stop();
#if DEBUG
            Events.Log($"TcpServer stopped.");
#endif

            isStopped = true;
        }

        public void Dispose()
        {
            if (isDisposed) return;

            Stop();
            server.Dispose();
            cts.Dispose();

            isDisposed = true;
        }
    }
}
