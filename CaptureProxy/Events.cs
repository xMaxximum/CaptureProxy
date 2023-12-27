using CaptureProxy.MyEventArgs;
using System.Diagnostics;

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
        public static event EventHandler<BeforeTunnelConnectEventArgs> BeforeTunnelConnect = delegate { };

        /// <summary>
        /// Event triggered before sending an HTTP request to remote server.
        /// </summary>
        public static event EventHandler<BeforeRequestEventArgs> BeforeRequest = delegate { };

        /// <summary>
        /// Event triggered before client receiving an HTTP response.
        /// </summary>
        public static event EventHandler<BeforeResponseEventArgs> BeforeResponse = delegate { };

        public static void Log(string message)
        {
            Debug.WriteLine(message);
            if (Logger != null) Logger(message);
        }

        internal static void HandleSessionConnected(object sender, SessionConnectedEventArgs e)
        {
            SessionConnected?.Invoke(sender, e);
        }

        internal static void HandleSessionDisconnected(object sender, SessionDisconnectedEventArgs e)
        {
            SessionDisconnected?.Invoke(sender, e);
        }

        internal static void HandleBeforeTunnelConnect(object sender, BeforeTunnelConnectEventArgs e)
        {
            BeforeTunnelConnect?.Invoke(sender, e);
        }

        internal static void HandleBeforeRequest(object sender, BeforeRequestEventArgs e)
        {
            BeforeRequest?.Invoke(sender, e);
        }

        internal static void HandleBeforeResponse(object sender, BeforeResponseEventArgs e)
        {
            BeforeResponse?.Invoke(sender, e);
        }
    }
}
