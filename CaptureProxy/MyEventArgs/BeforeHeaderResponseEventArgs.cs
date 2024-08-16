using CaptureProxy.HttpIO;

namespace CaptureProxy.MyEventArgs
{
    /// <summary>
    /// Custom EventArgs class for events triggered before an HTTP response.
    /// </summary>
    public class BeforeHeaderResponseEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the HTTP request associated with the event.
        /// </summary>
        public HttpRequest Request { get; private set; }

        /// <summary>
        /// Gets the HTTP response associated with the event.
        /// </summary>
        public HttpResponse Response { get; private set; }

        /// <summary>
        /// <para>
        /// Gets or sets a flag indicating whether the response should be captured.
        /// </para>
        /// <para>
        /// Set this property to <c>true</c> to enable capturing the body response
        /// and trigger the <see cref="Events.BeforeBodyResponse"/> event.
        /// </para>
        /// </summary>
        public bool CaptureBody { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="BeforeResponseEventArgs"/> class.
        /// </summary>
        /// <param name="request">The HTTP request associated with the event.</param>
        /// <param name="response">The HTTP response associated with the event.</param>
        public BeforeHeaderResponseEventArgs(HttpRequest request, HttpResponse response)
        {
            Request = request;
            Response = response;
        }
    }
}
