namespace CaptureProxy
{
    public static class Settings
    {
        public static int ReadTimeoutMs = 30000;
        public static int MaxIncomingHeaderLine = 64 * 1024;
        public static int StreamBufferSize = 64 * 1024;
        public static int MaxChunkSizeLine = 1024;
    }
}
