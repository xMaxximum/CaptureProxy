using CaptureProxy;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CaptureProxyTests
{
    public class CapturedUnitTests
    {
        private int port = new Random().Next(10000, ushort.MaxValue);

        private HttpProxy proxy;
        private HttpClient client;

        [SetUp]
        public void Setup()
        {
            Events.BeforeTunnelEstablish += Events_BeforeTunnelConnect;

            proxy = new HttpProxy(port);
            proxy.Start();

            client = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy($"localhost:{port}", false),
                UseProxy = true,
                ServerCertificateCustomValidationCallback = RemoteCertificateValidationCallback,
            });
        }

        private void Events_BeforeTunnelConnect(object? sender, CaptureProxy.MyEventArgs.BeforeTunnelEstablishEventArgs e)
        {
            e.PacketCapture = true;
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

        [Test]
        public async Task RequestModifyTest()
        {
            Events.BeforeRequest += RequestModifyTest_BeforeRequest;

            using HttpContent content = new StringContent("lorem ipsum", Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync("https://reqres.in/api/users", content);
            response.EnsureSuccessStatusCode();
            Assert.Pass();

            Events.BeforeRequest -= RequestModifyTest_BeforeRequest;
        }

        private void RequestModifyTest_BeforeRequest(object? sender, CaptureProxy.MyEventArgs.BeforeRequestEventArgs e)
        {
            e.Request.SetBody("{\"name\":\"morpheus\",\"job\":\"leader\"}");
        }

        [Test]
        public async Task ResponseModifyTest()
        {
            Events.BeforeHeaderResponse += Events_BeforeHeaderResponse;
            Events.BeforeBodyResponse += Events_BeforeBodyResponse;

            using HttpResponseMessage response = await client.GetAsync("https://anglesharp.azurewebsites.net/Chunked");

            string responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Is.EqualTo("Response modified."));

            Events.BeforeHeaderResponse -= Events_BeforeHeaderResponse;
            Events.BeforeBodyResponse -= Events_BeforeBodyResponse;
        }

        private void Events_BeforeHeaderResponse(object? sender, CaptureProxy.MyEventArgs.BeforeHeaderResponseEventArgs e)
        {
            e.CaptureBody = true;
        }

        private void Events_BeforeBodyResponse(object? sender, CaptureProxy.MyEventArgs.BeforeBodyResponseEventArgs e)
        {
            e.Response.SetBody("Response modified.");
        }
    }
}