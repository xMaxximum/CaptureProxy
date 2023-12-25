using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    internal abstract class Tunnel
    {
        public HttpRequest? RequestHeader { get; set; }

        public abstract Task StartAsync();
        public abstract void Stop();
    }
}
