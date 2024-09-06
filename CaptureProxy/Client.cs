using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CaptureProxy
{
    public class Client : IDisposable
    {
        private readonly TcpClient client;
        private readonly HttpProxy proxy;

        public Stream Stream { get; private set; }

        public string IpPort { get; private set; }

        public Client(HttpProxy proxy, TcpClient client)
        {
            this.proxy = proxy;

            this.client = client;
            Stream = this.client.GetStream();

            IpPort = this.client.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
        }

        public bool Connected => client.Connected;

        public void AuthenticateAsClient(string host)
        {
            var sslStream = new SslStream(Stream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
            sslStream.AuthenticateAsClient(host, null, false);

            Stream = sslStream;
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void AuthenticateAsServer(string host)
        {
            var certificate = CertMaker.CreateCertificate(CertMaker.CaCert, host);

            var sslStream = new SslStream(Stream, false);
            sslStream.AuthenticateAsServer(certificate, false, false);

            Stream = sslStream;
        }

        public void Close()
        {
            Stream.Close();
            client.Close();
        }

        public void Dispose()
        {
            Close();

            Stream.Dispose();
            client.Dispose();
        }

        public async Task<string> ReadLineAsync(long maxLength = 1024)
        {
            var buffer = new byte[maxLength];
            int bufferLength = 0;
            int bytesRead = 0;
            bool successful = false;

            while (true)
            {
                if (bufferLength >= maxLength) break;
                if (proxy.Token.IsCancellationRequested) break;

                bytesRead = await Stream.ReadAsync(buffer.AsMemory(bufferLength, 1), proxy.Token).ConfigureAwait(false);
                if (bytesRead == 0) throw new OperationCanceledException("Stream return no data.");

                bufferLength += bytesRead;
                if (bufferLength < 2) continue;

                if (buffer[bufferLength - 2] == '\r' && buffer[bufferLength - 1] == '\n')
                {
                    successful = true;
                    break;
                }
            }

            if (!successful)
            {
                throw new OverflowException("Data read is overflow max size of buffer.");
            }

            return Encoding.UTF8.GetString(buffer, 0, bufferLength - 2);
        }

        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken? cancellationToken = null)
        {
            cancellationToken = cancellationToken ?? proxy.Token;

            if (!Stream.CanRead) throw new InvalidOperationException("Input stream is not readable.");
            if (buffer.Length < 1) throw new ArgumentException("Input buffer length cannot be zero length.");

            int bytesRead = await Stream.ReadAsync(buffer, cancellationToken.Value).ConfigureAwait(false);
            if (bytesRead == 0) throw new OperationCanceledException("Stream return no data.");

            return bytesRead;
        }

        public async Task WriteAsync(Memory<byte> buffer, CancellationToken? cancellationToken = null)
        {
            cancellationToken = cancellationToken ?? proxy.Token;

            if (!Stream.CanWrite) throw new InvalidOperationException("Input stream is not writable.");
            if (buffer.Length < 1) throw new ArgumentException("Input buffer length cannot be zero length.");

            await Stream.WriteAsync(buffer, cancellationToken.Value).ConfigureAwait(false);
        }

        public async Task<byte[]> ReadExtractAsync(long length)
        {
            var buffer = new byte[length];
            int bytesRead = 0;
            int remainingBytes = 0;

            while (true)
            {
                if (proxy.Token.IsCancellationRequested) break;

                remainingBytes = buffer.Length - bytesRead;
                if (remainingBytes <= 0) break;

                bytesRead += await ReadAsync(buffer.AsMemory(bytesRead)).ConfigureAwait(false);
            }

            return buffer;
        }
    }
}
