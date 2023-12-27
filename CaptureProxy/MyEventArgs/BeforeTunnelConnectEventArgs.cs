using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy.MyEventArgs
{
    public class BeforeTunnelConnectEventArgs : EventArgs
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Abort { get; set; } = false;
        public bool UpstreamProxy { get; set; } = false;
        public string? ProxyUser { get; set; } = null;
        public string? ProxyPass { get; set; } = null;
        public bool PacketCapture { get; set; } = false;

        public BeforeTunnelConnectEventArgs(string host, int port)
        {
            Host = host;
            Port = port;
        }
    }
}
