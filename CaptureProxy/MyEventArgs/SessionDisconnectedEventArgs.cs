using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy.MyEventArgs
{
    public class SessionDisconnectedEventArgs : EventArgs
    {
        public Session Session { get; private set; }

        public SessionDisconnectedEventArgs(Session session)
        {
            Session = session;
        }
    }
}
