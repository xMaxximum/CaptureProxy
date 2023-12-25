using System.Net;
using System.Text;

namespace CaptureProxy
{
    public class HttpResponse : IDisposable
    {
        private bool _chunkedTransfer = false;

        public string Version { get; set; } = "HTTP/1.1";
        public HttpStatusCode StatusCode { get; set; }
        public string? ReasonPhrase { get; set; }
        public HeaderCollection Headers { get; } = new HeaderCollection();
        public byte[]? Body { get; private set; }

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
            if (lineSplit.Length < 2) throw new ArgumentException("Response line does not contain at least two parts (version, status, [reason pharse]).");

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
                if (splitOffet == -1) throw new ArgumentException("Response header does not contain at least two parts (key: value).");

                string key = line.Substring(0, splitOffet).Trim().ToLower();
                if (string.IsNullOrEmpty(key)) continue;

                string val = line.Substring(splitOffet + 1).Trim();

                if (key.Equals("transfer-encoding") && val.ToLower().Equals("chunked"))
                {
                    _chunkedTransfer = true;
                }

                Headers.Add(key, val);
            }
        }

        public async Task WriteHeaderAsync(Stream stream, CancellationToken token)
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

        public void SetBody(string body)
        {
            SetBody(body, "text/html", Encoding.UTF8);
        }

        public void SetBody(string body, string mediaType, Encoding encoding)
        {
            Body = encoding.GetBytes(body);
            Headers.AddOrReplace("Content-Type", $"{mediaType}; charset={encoding}");
            Headers.AddOrReplace("Content-Length", Body.Length.ToString());
        }

        public async Task WriteBodyAsync(Stream stream, CancellationToken token)
        {
            await stream.WriteAsync(Body, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
    }
}
