using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace CaptureProxy
{
    internal class Client : IDisposable
    {
        private TcpClient _client;

        public Stream Stream { get; private set; }

        public string IpPort => _client.Client?.RemoteEndPoint?.ToString() ?? string.Empty;

        public Client(TcpClient tcpClient)
        {
            _client = tcpClient;
            Stream = _client.GetStream();
        }

        public void AuthenticateAsClient(string host)
        {
            SslStream sslStream = new SslStream(Stream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
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

            SslStream sslStream = new SslStream(Stream, false);
            sslStream.AuthenticateAsServer(certificate, false, false);

            Stream = sslStream;
        }

        public void Close()
        {
            Stream.Close();
            _client.Close();
        }

        public void Dispose()
        {
            Close();

            Stream.Dispose();
            _client.Dispose();
        }
    }
}
