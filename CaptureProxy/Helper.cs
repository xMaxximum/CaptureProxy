using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace CaptureProxy
{
    internal class Helper
    {
        public static bool IsUserAdmin()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // Kiểm tra xem người dùng có phải là quản trị viên hay không
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static async Task<int> StreamReadAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (!stream.CanRead) throw new InvalidOperationException("Input stream is not readable.");
            if (buffer.Length < 1) return 0;

            int bytesRead = await stream.ReadAsync(buffer, offset, count, token).ConfigureAwait(false);
            if (bytesRead == 0) throw new OperationCanceledException("Stream return no data.");

            return bytesRead;
        }

        public static async Task<int> StreamReadAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            return await StreamReadAsync(stream, buffer, 0, buffer.Length, token).ConfigureAwait(false);
        }

        public static async Task<string> StreamReadLineAsync(Stream stream, long maxLength = 1024, CancellationToken token = default)
        {
            byte[] buffer = new byte[maxLength];
            int bufferLength = 0;
            int bytesRead = 0;
            bool successful = false;

            while (!token.IsCancellationRequested)
            {
                if (bufferLength >= maxLength) break;

                bytesRead = await stream.ReadAsync(buffer, bufferLength, 1, token).ConfigureAwait(false);
                if (bytesRead == 0) throw new OperationCanceledException("Stream return no data.");

                bufferLength += bytesRead;
                if (bufferLength < 2) continue;

                if (buffer[bufferLength - 2] == '\r' && buffer[bufferLength - 1] == '\n')
                {
                    successful = true;
                    break;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, successful ? (bufferLength - 2) : bufferLength);
        }

        public static async Task<byte[]> StreamReadExactlyAsync(Stream stream, long length, CancellationToken token)
        {
            if (!stream.CanRead) throw new InvalidOperationException("Input stream is not readable.");
            if (length < 1) return Array.Empty<byte>();

            using MemoryStream ms = new MemoryStream();
            long bytesRemaining = length;

            int bytesRead = 0;
            int bufferSize = 0;
            byte[] buffer = new byte[Settings.StreamBufferSize];
            while (!token.IsCancellationRequested && bytesRemaining > 0)
            {
                bufferSize = (int)Math.Min(bytesRemaining, buffer.Length);
                bytesRead = await StreamReadAsync(stream, buffer, 0, bufferSize, token).ConfigureAwait(false);
                ms.Write(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }

            return ms.ToArray();
        }

        public static T DeepClone<T>(T obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
