namespace CaptureProxy.MyEventArgs
{
    /// <summary>
    /// Custom EventArgs class for events triggered before a tunnel connection.
    /// </summary>
    public class BeforeTunnelEstablishEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the hostname or IP address of the target.
        /// </summary>
        public string Host { get; internal set; }

        /// <summary>
        /// Gets the port number for the connection.
        /// </summary>
        public int Port { get; internal set; }

        /// <summary>
        /// Gets or sets a flag indicating whether the tunnel connection should be aborted.
        /// </summary>
        public bool Abort { get; set; } = false;

        /// <summary>
        /// Gets or sets a upstream proxy.
        /// </summary>
        public UpstreamHttpProxy? UpstreamProxy { get; set; } = null;

        /// <summary>
        /// Gets or sets a flag indicating whether packet capture is enabled for the connection.
        /// </summary>
        public bool PacketCapture { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="BeforeTunnelEstablishEventArgs"/> class.
        /// </summary>
        /// <param name="host">The hostname or IP address of the target.</param>
        /// <param name="port">The port number for the connection.</param>
        public BeforeTunnelEstablishEventArgs(string host, int port)
        {
            Host = host;
            Port = port;
        }
    }
}
