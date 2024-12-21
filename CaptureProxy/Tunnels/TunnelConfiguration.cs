using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Net;

namespace CaptureProxy.Tunnels
{
    internal class TunnelConfiguration
    {
        public Uri BaseUri { get; set; }
        public Client Client { get; set; }
        public BeforeTunnelEstablishEventArgs e { get; set; }
        public HttpClient? HttpClient { get; set; }
        public HttpRequest InitRequest { get; set; }
        public HttpProxy Proxy { get; set; }
        public Client Remote { get; set; }
    }
}