using System.Globalization;
using System.Net;
using System.Text;

namespace CaptureProxy
{
    public class HttpResponse : IDisposable
    {
        public bool ChunkedTransfer { get; set; } = false;

        public string Version { get; set; } = "HTTP/1.1";
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string? ReasonPhrase { get; set; } = "OK";
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
                    ChunkedTransfer = true;
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
            Headers.AddOrReplace("Content-Type", $"{mediaType}; charset={encoding.WebName}");
            Headers.AddOrReplace("Content-Length", Body.Length.ToString());
            if (Headers.GetAsFisrtValue("Transfer-Encoding") == "chunked")
            {
                Headers.Remove("Transfer-Encoding");
            }
        }

        public async Task WriteBodyAsync(Stream stream, CancellationToken token)
        {
            await stream.WriteAsync(Body, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        public async Task<byte[]> ReadChunkAsync(Stream stream, CancellationToken token)
        {
            string hexLength = await Helper.StreamReadLineAsync(stream, Settings.MaxChunkSizeLine, token).ConfigureAwait(false);
            if (int.TryParse(hexLength, NumberStyles.HexNumber, null, out int chunkSize) == false)
            {
                throw new InvalidOperationException($"Chunk size {hexLength} is not valid.");
            }

            byte[] buffer = await Helper.StreamReadExactlyAsync(stream, chunkSize, token).ConfigureAwait(false);
            string endOfChunk = await Helper.StreamReadLineAsync(stream, 2, token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(endOfChunk) == false)
            {
                throw new InvalidOperationException($"End of chunk {hexLength} is not CRLF bytes.");
            }

            return buffer;
        }

        public async Task ReadBodyAsync(Stream stream, CancellationToken token)
        {
            if (Headers.ContentLength > 0)
            {
                Body = await Helper.StreamReadExactlyAsync(stream, Headers.ContentLength.Value, token).ConfigureAwait(false);
            }
            else if (ChunkedTransfer)
            {
                using MemoryStream ms = new MemoryStream();

                while (!token.IsCancellationRequested)
                {
                    byte[] buffer = await ReadChunkAsync(stream, token).ConfigureAwait(false);
                    if (buffer.Length == 0) break;

                    ms.Write(buffer);
                }
            }
        }
    }
}
