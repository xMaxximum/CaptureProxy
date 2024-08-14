using CaptureProxy.HttpIO;

namespace CaptureProxy.MyEventArgs
{
    /// <summary>
    /// Custom EventArgs class for events triggered before an HTTP request.
    /// </summary>
    public class BeforeRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the HTTP request associated with the event.
        /// </summary>
        public HttpRequest Request { get; private set; }

        /// <summary>
        /// <para>
        /// Gets or sets the HTTP response associated with the event.
        /// </para>
        /// <para>
        /// If set, the request will not be sent to the remote server,
        /// and the response will be sent to the client immediately.
        /// </para>
        /// </summary>
        public HttpResponse? Response { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets a flag indicating whether the response should be captured.
        /// </para>
        /// <para>
        /// Set this property to <c>true</c> to enable capturing the response
        /// and trigger the <see cref="Events.BeforeResponse"/> event.
        /// </para>
        /// </summary>
        public bool CaptureResponse { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="BeforeRequestEventArgs"/> class.
        /// </summary>
        /// <param name="request">The HTTP request associated with the event.</param>
        public BeforeRequestEventArgs(HttpRequest request)
        {
            Request = request;
        }
    }
}
