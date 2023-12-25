using CaptureProxy.MyEventArgs;
using System.Diagnostics;

namespace CaptureProxy
{
    public static class Events
    {
        public static Action<string>? Logger { get; set; } = null;
        public static event EventHandler<SessionConnectedEventArgs> SessionConnected = delegate { };
        public static event EventHandler<SessionDisconnectedEventArgs> SessionDisconnected = delegate { };

        public static void Log(string message)
        {
            Debug.WriteLine(message);
            if (Logger != null) Logger(message);
        }

        public static void HandleSessionConnected(object sender, SessionConnectedEventArgs e)
        {
            SessionConnected?.Invoke(sender, e);
        }

        public static void HandleSessionDisconnected(object sender, SessionDisconnectedEventArgs e)
        {
            SessionDisconnected?.Invoke(sender, e);
        }
    }
}
