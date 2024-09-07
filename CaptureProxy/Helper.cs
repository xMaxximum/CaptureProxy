using CaptureProxy.HttpIO;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace CaptureProxy
{
    internal static class Helper
    {
        public static async Task SendConnectedResponse(HttpProxy proxy, Client client)
        {
            using var response = new HttpResponse(proxy);
            response.Version = "HTTP/1.1";
            response.StatusCode = HttpStatusCode.OK;
            response.ReasonPhrase = "Connection Established";
            await response.WriteHeaderAsync(client).ConfigureAwait(false);
        }

        public static async Task SendBadGatewayResponse(HttpProxy proxy, Client client)
        {
            using var response = new HttpResponse(proxy);
            response.Version = "HTTP/1.1";
            response.StatusCode = HttpStatusCode.BadGateway;
            response.ReasonPhrase = "Bad Gateway";
            await response.WriteHeaderAsync(client).ConfigureAwait(false);
        }
    }
}
