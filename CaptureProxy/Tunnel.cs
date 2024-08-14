using CaptureProxy.HttpIO;

namespace CaptureProxy
{
    internal abstract class Tunnel : IDisposable
    {
        public HttpRequest? RequestHeader { get; set; }

        public abstract Task StartAsync();
        public abstract void Stop();
        protected abstract bool ShouldStop();
        public abstract void Dispose();
    }
}
