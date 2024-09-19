using CaptureProxy.MyEventArgs;
using System.Text;

namespace CaptureProxy
{
    public class Events
    {
        /// <summary>
        /// Event triggered when log received.
        /// </summary>
        public event EventHandler<string>? LogReceived;

        /// <summary>
        /// Event triggered when a session is connected.
        /// </summary>
        public event EventHandler<SessionConnectedEventArgs>? SessionConnected;

        /// <summary>
        /// Event triggered when a session is disconnected.
        /// </summary>
        public event EventHandler<SessionDisconnectedEventArgs>? SessionDisconnected;

        /// <summary>
        /// Event triggered before establishing a tunnel connection.
        /// </summary>
        public event EventHandler<BeforeTunnelEstablishEventArgs>? BeforeTunnelEstablish;

        /// <summary>
        /// Event triggered before sending an HTTP request to remote server.
        /// </summary>
        public event EventHandler<BeforeRequestEventArgs>? BeforeRequest;

        /// <summary>
        /// Event triggered before client receiving an HTTP header response.
        /// </summary>
        public event EventHandler<BeforeHeaderResponseEventArgs>? BeforeHeaderResponse;

        /// <summary>
        /// Event triggered before client receiving an HTTP body response.
        /// </summary>
        public event EventHandler<BeforeBodyResponseEventArgs>? BeforeBodyResponse;

        internal void Log(string message)
        {
            if (LogReceived == null) return;
            LogReceived?.Invoke(this, message);
        }

        internal void Log(Exception ex)
        {
            if (LogReceived == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("EXCEPTION");
            sb.AppendLine($"Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Stack Trace: {ex.StackTrace}");

            LogReceived.Invoke(this, sb.ToString());
        }

        internal void HandleSessionConnected(object sender, SessionConnectedEventArgs e)
        {
            SessionConnected?.Invoke(sender, e);
        }

        internal void HandleSessionDisconnected(object sender, SessionDisconnectedEventArgs e)
        {
            SessionDisconnected?.Invoke(sender, e);
        }

        internal void HandleBeforeTunnelEstablish(object sender, BeforeTunnelEstablishEventArgs e)
        {
            BeforeTunnelEstablish?.Invoke(sender, e);
        }

        internal void HandleBeforeRequest(object sender, BeforeRequestEventArgs e)
        {
            BeforeRequest?.Invoke(sender, e);
        }

        internal void HandleBeforeHeaderResponse(object sender, BeforeHeaderResponseEventArgs e)
        {
            BeforeHeaderResponse?.Invoke(sender, e);
        }

        internal void HandleBeforeBodyResponse(object sender, BeforeBodyResponseEventArgs e)
        {
            BeforeBodyResponse?.Invoke(sender, e);
        }
    }
}
