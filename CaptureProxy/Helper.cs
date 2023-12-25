using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    internal class Helper
    {
        public static bool IsUserAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // Kiểm tra xem người dùng có phải là quản trị viên hay không
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static async Task<byte[]> StreamReadAsync(Stream stream, long length, CancellationToken token)
        {
            if (!stream.CanRead) throw new InvalidOperationException("Input stream is not readable.");
            if (length < 1) return new byte[0];

            using MemoryStream ms = new MemoryStream();
            long bytesRemaining = length;
            byte[] buffer = new byte[Math.Min(bytesRemaining, Settings.StreamBufferSize)];
            int bytesRead = 0;

            while (!token.IsCancellationRequested && bytesRemaining > 0)
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead == 0) throw new InvalidOperationException("Stream return no data.");

                ms.Write(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }

            return ms.ToArray();
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
                if (bytesRead == 0) throw new InvalidOperationException("Stream return no data.");

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
    }
}
