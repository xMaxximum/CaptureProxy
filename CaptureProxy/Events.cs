using CaptureProxy.MyEventArgs;
using System.Diagnostics;

namespace CaptureProxy
{
    public static class Events
    {
        public static Action<string>? Logger { get; set; } = null;
        public static event EventHandler<SessionConnectedEventArgs> SessionConnected = delegate { };
        public static event EventHandler<SessionDisconnectedEventArgs> SessionDisconnected = delegate { };
        public static event EventHandler<EstablishRemoteEventArgs> EstablishRemote = delegate { };
        public static event EventHandler<BeforeRequestEventArgs> BeforeRequest = delegate { };

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

        internal static void HandleEstablishRemote(object sender, EstablishRemoteEventArgs e)
        {
            EstablishRemote?.Invoke(sender, e);
        }

        internal static void HandleBeforeRequest(object sender, BeforeRequestEventArgs e)
        {
            BeforeRequest?.Invoke(sender, e);
        }
    }
}
