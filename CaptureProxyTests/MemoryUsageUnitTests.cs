using CaptureProxy;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CaptureProxyTests
{
    public class MemoryUsageUnitTests
    {
        private int port = new Random().Next(10000, ushort.MaxValue);

        private HttpProxy proxy;
        private HttpClient client;

        [SetUp]
        public void Setup()
        {
            proxy = new HttpProxy(port);
            proxy.Start();

            client = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy($"localhost:{port}", false),
                UseProxy = true,
                ServerCertificateCustomValidationCallback = RemoteCertificateValidationCallback,
            });
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        [TearDown]
        public void Cleanup()
        {
            proxy.Stop();
            proxy.Dispose();

            client.Dispose();
        }

        [Test]
        public async Task TextEventStreamTest()
        {
            long startMemory = GC.GetTotalMemory(true);

            try
            {
                client.Timeout = TimeSpan.FromMinutes(1);
                await client.GetAsync("https://stream.wikimedia.org/v2/stream/recentchange");
            }
            catch { }

            long endMemory = GC.GetTotalMemory(true);
            var usedMemory = Math.Round((endMemory - startMemory) / 1024.0 / 1024, 2);

            Assert.Pass($"Used Memory: {usedMemory} MiB.");
        }
    }
}