using CaptureProxy;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CaptureProxyTests
{
    public class NonCaptureUnitTests
    {
        private int port = new Random().Next(10000, ushort.MaxValue);
        private HttpProxy proxy;
        private HttpClient client;

        [OneTimeSetUp]
        public void Setup()
        {
            CaptureProxy.Events.Logger = (string message) =>
            {
                message = $"[{DateTime.Now}] {message}\r\n";
                File.AppendAllText("logs.txt", message);
            };

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

        [OneTimeTearDown]
        public void Cleanup()
        {
            proxy.Dispose();
            client.Dispose();
        }

        [Test]
        public async Task HttpTest()
        {
            using HttpResponseMessage response = await client.GetAsync("http://example.com");
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            if (responseBody.Trim().EndsWith("</html>") == false)
            {
                Assert.Fail("Response body is not ends with html close tag.");
            }

            Assert.Pass();
        }

        [Test]
        public async Task HttpsTest()
        {
            using HttpResponseMessage response = await client.GetAsync("https://example.com");
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            if (responseBody.Trim().EndsWith("</html>") == false)
            {
                Assert.Fail("Response body is not ends with html close tag.");
            }

            Assert.Pass();
        }

        [Test]
        public async Task AbortTest()
        {
            Events.BeforeTunnelEstablish += AbortTest_BeforeTunnelEstablish;

            try
            {
                using HttpResponseMessage response = await client.GetAsync("https://example.com");
            }
            catch (HttpRequestException)
            {
                Assert.Pass();
            }
            finally
            {
                Events.BeforeTunnelEstablish -= AbortTest_BeforeTunnelEstablish;
            }
        }

        private void AbortTest_BeforeTunnelEstablish(object? sender, CaptureProxy.MyEventArgs.BeforeTunnelEstablishEventArgs e)
        {
            e.Abort = true;
        }

        [Test]
        public async Task SubmitTest()
        {
            using HttpContent content = new StringContent("{\"name\":\"morpheus\",\"job\":\"leader\"}", Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync("https://reqres.in/api/users", content);
            response.EnsureSuccessStatusCode();
            Assert.Pass();
        }
    }
}