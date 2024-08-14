using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;

namespace CaptureProxy.Tunnels
{
    internal class TunnelConfiguration
    {
        public required Client Client { get; set; }
        public required Client Remote { get; set; }
        public Uri? BaseUri { get; set; }
        public BeforeTunnelEstablishEventArgs? TunnelEstablishEvent { get; set; }
        public HttpRequest? InitRequest { get; set; }
    }
}
