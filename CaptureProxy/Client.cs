using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CaptureProxy
{
    public class Client : IDisposable
    {
        private TcpClient _client;

        private Stream _stream;
        public Stream Stream { get => _stream; }

        public string IpPort { get; private set; }

        public Client(TcpClient client)
        {
            _client = client;
            _stream = _client.GetStream();

            IpPort = _client.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
        }

        public void AuthenticateAsClient(string host)
        {
            var sslStream = new SslStream(_stream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
            sslStream.AuthenticateAsClient(host, null, false);

            _stream = sslStream;
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void AuthenticateAsServer(string host)
        {
            var certificate = CertMaker.CreateCertificate(CertMaker.CaCert, host);

            var sslStream = new SslStream(_stream, false);
            sslStream.AuthenticateAsServer(certificate, false, false);

            _stream = sslStream;
        }

        public void Close()
        {
            _stream.Close();
            _client.Close();
        }

        public void Dispose()
        {
            Close();

            _stream.Dispose();
            _client.Dispose();
        }

        public async Task<string> ReadLineAsync(long maxLength = 1024)
        {
            byte[] buffer = new byte[maxLength];
            int bufferLength = 0;
            int bytesRead = 0;
            bool successful = false;

            while (Settings.ProxyIsRunning)
            {
                if (bufferLength >= maxLength) break;

                bytesRead = await _stream.ReadAsync(buffer.AsMemory(bufferLength, 1));
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

        public async Task<int> ReadAsync(Memory<byte> buffer)
        {
            if (!_stream.CanRead) throw new InvalidOperationException("Input stream is not readable.");
            if (buffer.Length < 1) throw new ArgumentException("Input buffer length cannot be zero length.");

            int bytesRead = await _stream.ReadAsync(buffer);
            if (bytesRead == 0) throw new OperationCanceledException("Stream return no data.");

            return bytesRead;
        }

        public async Task<byte[]> ReadExtractAsync(long length)
        {
            var buffer = new byte[length];
            int bytesRead = 0;
            int remainingBytes = 0;

            while (Settings.ProxyIsRunning)
            {
                remainingBytes = buffer.Length - bytesRead;
                if (remainingBytes == 0) break;

                bytesRead += await ReadAsync(buffer.AsMemory(bytesRead));
            }

            return buffer;
        }
    }
}
