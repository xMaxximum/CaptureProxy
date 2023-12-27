using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy.MyEventArgs
{
    public class BeforeResponseEventArgs : EventArgs
    {
        public HttpRequest Request { get; private set; }
        public HttpResponse Response { get; private set; }

        public BeforeResponseEventArgs(HttpRequest request, HttpResponse response)
        {
            Request = request;
            Response = response;
        }
    }
}
