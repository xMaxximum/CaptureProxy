namespace CaptureProxy
{
    public class Settings
    {
        public int ConnectTimeout { get; set; } = 30;
        public int ReadTimeout { get; set; } = 30000;
        public int MaxIncomingHeaderLine { get; set; } = 64 * 1024;
        public int StreamBufferSize { get; set; } = 64 * 1024;
        public int MaxChunkSizeLine { get; set; } = 64 * 1024;
    }
}
