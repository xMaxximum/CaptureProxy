using CaptureProxy.HttpIO;

namespace CaptureProxy.MyEventArgs
{
    /// <summary>
    /// Custom EventArgs class for events triggered before an HTTP response.
    /// </summary>
    public class BeforeBodyResponseEventArgs : EventArgs
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
        /// Initializes a new instance of the <see cref="BeforeResponseEventArgs"/> class.
        /// </summary>
        /// <param name="request">The HTTP request associated with the event.</param>
        /// <param name="response">The HTTP response associated with the event.</param>
        public BeforeBodyResponseEventArgs(HttpRequest request, HttpResponse response)
        {
            Request = request;
            Response = response;
        }
    }
}
