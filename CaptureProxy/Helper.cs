using System.Security.Principal;
using System.Text;

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

        public static async Task<byte[]> StreamReadAsync(Stream stream, long bufferSize, CancellationToken token)
        {
            if (!stream.CanRead) throw new InvalidOperationException("Input stream is not readable.");
            if (bufferSize < 1) return new byte[0];

            byte[] buffer = new byte[Math.Min(bufferSize, Settings.StreamBufferSize)];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            if (bytesRead == 0) throw new OperationCanceledException("Stream return no data.");

            byte[] data = new byte[bytesRead];
            Array.Copy(buffer, data, bytesRead);

            return data;
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
            if (length < 1) return new byte[0];

            using MemoryStream ms = new MemoryStream();
            long bytesRemaining = length;

            while (!token.IsCancellationRequested && bytesRemaining > 0)
            {
                byte[] buffer = await StreamReadAsync(stream, bytesRemaining, token).ConfigureAwait(false);
                ms.Write(buffer, 0, buffer.Length);
                bytesRemaining -= buffer.Length;
            }

            return ms.ToArray();
        }
    }
}
