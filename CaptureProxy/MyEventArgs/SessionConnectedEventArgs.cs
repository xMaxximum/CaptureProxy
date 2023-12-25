using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy.MyEventArgs
{
    public class SessionConnectedEventArgs : EventArgs
    {
        public Session Session { get; private set; }

        public SessionConnectedEventArgs(Session session)
        {
            Session = session;
        }
    }
}
