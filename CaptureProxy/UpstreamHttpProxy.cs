using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    public enum ProxyType
    {
        HTTP,
        SOCKS5
    }

    public class UpstreamHttpProxy
    {
        /// <summary>
        /// Gets or sets the hostname or IP address of the upstream proxy.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the optional password for proxy authentication.
        /// </summary>
        public string? Pass { get; set; } = null;

        /// <summary>
        /// Gets or sets the port number of the upstream proxy.
        /// </summary>
        public int Port { get; set; }

        public ProxyType ProxyType { get; set; }

        /// <summary>
        /// Gets or sets the optional username for proxy authentication.
        /// </summary>
        public string? User { get; set; } = null;
    }
}