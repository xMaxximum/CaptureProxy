using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy.MyEventArgs
{
    public class BeforeRequestEventArgs : EventArgs
    {
        public HttpRequest Request { get; private set; }
        public HttpResponse? Response { get; set; }
        public bool CaptureResponse { get; set; } = false;

        public BeforeRequestEventArgs(HttpRequest request)
        {
            Request = request;
        }
    }
}
