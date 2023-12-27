namespace CaptureProxy.MyEventArgs
{
    /// <summary>
    /// Custom EventArgs class for events triggered before a tunnel connection.
    /// </summary>
    public class BeforeTunnelConnectEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the hostname or IP address of the target.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the port number for the connection.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether the tunnel connection should be aborted.
        /// </summary>
        public bool Abort { get; set; } = false;

        /// <summary>
        /// Gets or sets a flag indicating the use of an upstream proxy.
        /// </summary>
        public bool UpstreamProxy { get; set; } = false;

        /// <summary>
        /// Gets or sets the optional username for proxy authentication.
        /// </summary>
        public string? ProxyUser { get; set; } = null;

        /// <summary>
        /// Gets or sets the optional password for proxy authentication.
        /// </summary>
        public string? ProxyPass { get; set; } = null;

        /// <summary>
        /// Gets or sets a flag indicating whether packet capture is enabled for the connection.
        /// </summary>
        public bool PacketCapture { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="BeforeTunnelConnectEventArgs"/> class.
        /// </summary>
        /// <param name="host">The hostname or IP address of the target.</param>
        /// <param name="port">The port number for the connection.</param>
        public BeforeTunnelConnectEventArgs(string host, int port)
        {
            Host = host;
            Port = port;
        }
    }
}
