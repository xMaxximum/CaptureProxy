using System.Diagnostics;

namespace CaptureProxy
{
    public static class Events
    {
        public static Action<string> Logger = delegate { };

        public static void Log(string message)
        {
            Debug.WriteLine(message);
            Logger(message);
        }
    }
}
