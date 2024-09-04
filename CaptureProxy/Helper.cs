using CaptureProxy.HttpIO;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace CaptureProxy
{
    internal static class Helper
    {
        public static T DeepClone<T>(T obj)
        {
            var json = JsonSerializer.Serialize(obj);
#pragma warning disable CS8603 // Possible null reference return.
            return JsonSerializer.Deserialize<T>(json);
#pragma warning restore CS8603 // Possible null reference return.
        }

        public static async Task SendConnectedResponse(Client client)
        {
            using HttpResponse response = new HttpResponse();
            response.Version = "HTTP/1.1";
            response.StatusCode = HttpStatusCode.OK;
            response.ReasonPhrase = "Connection Established";
            await response.WriteHeaderAsync(client);
        }
    }
}
