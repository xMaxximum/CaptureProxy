using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy
{
    public static class Events
    {
        /// <summary>
        /// Logger property to handle logging actions.
        /// </summary>
        public static Action<string>? Logger { get; set; } = null;

        /// <summary>
        /// Event triggered when a session is connected.
        /// </summary>
        public static event EventHandler<SessionConnectedEventArgs> SessionConnected = delegate { };

        /// <summary>
        /// Event triggered when a session is disconnected.
        /// </summary>
        public static event EventHandler<SessionDisconnectedEventArgs> SessionDisconnected = delegate { };

        /// <summary>
        /// Event triggered before establishing a tunnel connection.
        /// </summary>
        public static event EventHandler<BeforeTunnelEstablishEventArgs> BeforeTunnelEstablish = delegate { };

        /// <summary>
        /// Event triggered before sending an HTTP request to remote server.
        /// </summary>
        public static event EventHandler<BeforeRequestEventArgs> BeforeRequest = delegate { };

        /// <summary>
        /// Event triggered before client receiving an HTTP header response.
        /// </summary>
        public static event EventHandler<BeforeHeaderResponseEventArgs> BeforeHeaderResponse = delegate { };

        /// <summary>
        /// Event triggered before client receiving an HTTP body response.
        /// </summary>
        public static event EventHandler<BeforeBodyResponseEventArgs> BeforeBodyResponse = delegate { };

        internal static void Log(string message)
        {
            if (Logger == null) return;
            Logger(message);
        }

        internal static void Log(Exception ex)
        {
            if (ex is OperationCanceledException) return;
            if (ex is IOException) return;

            if (Logger == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("EXCEPTION");
            sb.AppendLine($"Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Stack Trace: {ex.StackTrace}");

            Logger(sb.ToString());
        }

        internal static void HandleSessionConnected(object sender, SessionConnectedEventArgs e)
        {
            SessionConnected?.Invoke(sender, e);
        }

        internal static void HandleSessionDisconnected(object sender, SessionDisconnectedEventArgs e)
        {
            SessionDisconnected?.Invoke(sender, e);
        }

        internal static void HandleBeforeTunnelEstablish(object sender, BeforeTunnelEstablishEventArgs e)
        {
            BeforeTunnelEstablish?.Invoke(sender, e);
        }

        internal static void HandleBeforeRequest(object sender, BeforeRequestEventArgs e)
        {
            BeforeRequest?.Invoke(sender, e);
        }

        internal static void HandleBeforeHeaderResponse(object sender, BeforeHeaderResponseEventArgs e)
        {
            BeforeHeaderResponse?.Invoke(sender, e);
        }

        internal static void HandleBeforeBodyResponse(object sender, BeforeBodyResponseEventArgs e)
        {
            BeforeBodyResponse?.Invoke(sender, e);
        }
    }
}
