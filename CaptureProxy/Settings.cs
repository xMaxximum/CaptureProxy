namespace CaptureProxy
{
    public class Settings
    {
        public int ReadTimeoutMs = 30000;
        public int MaxIncomingHeaderLine = 64 * 1024;
        public int StreamBufferSize = 64 * 1024;
        public int MaxChunkSizeLine = 64 * 1024;
    }
}
