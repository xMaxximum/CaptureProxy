using System.Globalization;
using System.Net;
using System.Text;

namespace CaptureProxy
{
    public class HttpResponse : HttpPacket, IDisposable
    {
        public string Version { get; set; } = "HTTP/1.1";
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string? ReasonPhrase { get; set; } = "OK";
        public bool EventStream { get; set; } = false;

        public void Dispose()
        {
            Body = null;
            Headers.Dispose();
        }

        public async Task ReadHeaderAsync(Stream stream, CancellationToken token)
        {
            // Process first Line
            string line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
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
            while (!token.IsCancellationRequested)
            {
                line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
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

        public override async Task WriteHeaderAsync(Stream stream, CancellationToken token)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"{Version} {Convert.ToInt16(StatusCode)}");
            if (ReasonPhrase != null)
            {
                sb.Append($" {ReasonPhrase}");
            }
            sb.Append("\r\n");

            foreach (var item in Headers.GetAll())
            {
                foreach (var value in item.Value)
                {
                    sb.Append($"{item.Key}: {value}\r\n");
                }
            }

            sb.Append("\r\n");

            await stream.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()), token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        public async Task ReadEventStreamBody(Stream stream, CancellationToken token)
        {
            if (!EventStream) throw new InvalidOperationException("Response is not a event-stream content type.");

            if (Headers.ContentLength > 0)
            {
                await ReadBodyAsync(stream, token).ConfigureAwait(false);
                return;
            }

            if (ChunkedTransfer)
            {
                Body = await ReadChunkAsync(stream, token).ConfigureAwait(false);
            }
        }

        public async Task WriteEventStreamBody(Stream stream, CancellationToken token)
        {
            if (!EventStream) throw new InvalidOperationException("Response is not a event-stream content type.");

            if (Body == null) return;

            if (Headers.ContentLength > 0)
            {
                await WriteBodyAsync(stream, token).ConfigureAwait(false);
                return;
            }

            if (ChunkedTransfer)
            {
                await WriteChunkAsync(stream, Body, token).ConfigureAwait(false);
            }
        }
    }
}
