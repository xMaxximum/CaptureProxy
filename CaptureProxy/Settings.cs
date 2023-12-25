using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    public static class Settings
    {
        public static int ReadTimeoutMs = 30000;
        public static int MaxIncomingHeaderLine = 4 * 1024;
        public static int StreamBufferSize = 64 * 1024;
        public static int MaxChunkSizeLine = 1024;
    }
}
