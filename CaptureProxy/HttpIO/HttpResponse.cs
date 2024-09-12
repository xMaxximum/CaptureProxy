using System.Globalization;
using System.Net;
using System.Text;

namespace CaptureProxy.HttpIO
{
    public class HttpResponse(HttpProxy proxy) : HttpPacket(proxy)
    {
        public string Version { get; set; } = "HTTP/1.1";
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string? ReasonPhrase { get; set; } = "OK";

        internal bool EventStream { get; set; } = false;

        internal async Task ReadHeaderAsync(Client client)
        {
            // Process first Line
            string line = await client.ReadLineAsync(proxy.Settings.MaxIncomingHeaderLine).ConfigureAwait(false);
            string[] lineSplit = line.Split(' ');
            if (lineSplit.Length < 2)
            {
                throw new ArgumentException("Response line does not contain at least two parts (version, status, [reason pharse]).");
            }

            Version = lineSplit[0];

            if (Enum.TryParse<HttpStatusCode>(lineSplit[1], out var statusCode) == false)
            {
                throw new ArgumentException("Response status code is not valid.");
            }
            StatusCode = statusCode;

            StringBuilder sb = new StringBuilder();
            for (int i = 2; i < lineSplit.Length; i++)
            {
                sb.Append(lineSplit[i] + " ");
            }
            if (sb.Length > 0)
            {
                ReasonPhrase = sb.ToString().Trim();
            }

            // Process subsequent Line
            while (true)
            {
                if (proxy.Token.IsCancellationRequested) break;

                line = await client.ReadLineAsync(proxy.Settings.MaxIncomingHeaderLine).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line)) break;

                int splitOffet = line.IndexOf(':');
                if (splitOffet == -1)
                {
                    throw new ArgumentException("Response header does not contain at least two parts (key: value).");
                }

                string key = line.Substring(0, splitOffet).Trim().ToLower();
                if (string.IsNullOrEmpty(key)) continue;

                string val = line.Substring(splitOffet + 1).Trim();

                if (key.Equals("transfer-encoding") && val.ToLower().Equals("chunked"))
                {
                    ChunkedTransfer = true;
                }

                if (key.Equals("content-type") && val.ToLower().StartsWith("text/event-stream"))
                {
                    EventStream = true;
                }

                Headers.Add(key, val);
            }
        }

        internal async Task WriteHeaderAsync(Client client)
        {
            var sb = new StringBuilder();

            sb.Append($"{Version} {Convert.ToInt16(StatusCode)}");
            if (ReasonPhrase != null)
            {
                sb.Append($" {ReasonPhrase}");
            }
            sb.Append("\r\n");

            if (EventStream || ChunkedTransfer)
            {
                Headers.Remove("Content-Length");
            }

            foreach (var item in Headers.GetAll())
            {
                foreach (var value in item.Value)
                {
                    sb.Append($"{item.Key}: {value}\r\n");
                }
            }

            sb.Append("\r\n");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            await client.Stream.WriteAsync(bytes, proxy.Token).ConfigureAwait(false);
            await client.Stream.FlushAsync(proxy.Token).ConfigureAwait(false);
        }

        internal async Task ReadEventStreamBody(Client client)
        {
            if (!EventStream) throw new InvalidOperationException("Response is not a event-stream content type.");

            if (Headers.ContentLength > 0)
            {
                await ReadBodyAsync(client).ConfigureAwait(false);
                return;
            }

            if (ChunkedTransfer)
            {
                Body = await ReadChunkAsync(client).ConfigureAwait(false);
                return;
            }
        }

        internal async Task WriteEventStreamBody(Client client)
        {
            if (!EventStream) throw new InvalidOperationException("Response is not a event-stream content type.");

            if (Body == null) return;

            if (Headers.ContentLength > 0)
            {
                await WriteBodyAsync(client).ConfigureAwait(false);
                return;
            }

            if (ChunkedTransfer)
            {
                await WriteChunkAsync(client, Body).ConfigureAwait(false);
            }
        }
    }
}
