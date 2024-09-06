using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;

namespace CaptureProxy.Tunnels
{
    internal class TunnelConfiguration
    {
        public required HttpProxy Proxy { get; set; }
        public required Uri BaseUri { get; set; }
        public required Client Client { get; set; }
        public required Client Remote { get; set; }
        public required BeforeTunnelEstablishEventArgs e { get; set; }
        public required HttpRequest InitRequest { get; set; }
    }
}
