using System.Net.Security;
using System.Text;

namespace CaptureProxy.HttpIO
{
    public class HttpRequest(HttpProxy proxy) : HttpPacket(proxy)
    {
        internal HttpMethod Method { get; set; } = HttpMethod.Get;

        private Uri? _uri;

        public Uri Uri
        {
            get
            {
                if (_uri == null)
                {
                    throw new InvalidOperationException("Please run ReadHeaderAsync first to read header from stream before using this property.");
                }

                return _uri;
            }
            set => _uri = value;
        }

        public string Version { get; set; } = "HTTP/1.1";

        internal async Task ReadHeaderAsync(Client client, Uri? baseUri = null)
        {
            // Process first Line
            string line = await client.ReadLineAsync(proxy.Settings.MaxChunkSizeLine).ConfigureAwait(false);
            string[] lineSplit = line.Split(' ');
            if (lineSplit.Length < 3)
            {
                throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");
            }

            // Store method
            Method = HttpMethod.Parse(lineSplit[0]);

            // Store url
            var url = (Method == HttpMethod.Connect ? "https://" : "") + lineSplit[1];
            if (!Uri.TryCreate(baseUri, url, out _uri))
            {
                throw new ArgumentException("Can not parse request url.");
            }

            // Store version
            Version = lineSplit[2];

            // Process subsequent Line
            while (true)
            {
                if (proxy.Token.IsCancellationRequested) break;

                line = await client.ReadLineAsync(proxy.Settings.MaxChunkSizeLine).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line)) break;

                int splitOffet = line.IndexOf(':');
                if (splitOffet == -1)
                {
                    throw new ArgumentException("Request header does not contain at least two parts (key: value).");
                }

                string key = line.Substring(0, splitOffet).Trim().ToLower();
                if (string.IsNullOrEmpty(key)) continue;

                string val = line.Substring(splitOffet + 1).Trim();

                if (key.Equals("transfer-encoding") && val.ToLower().Equals("chunked"))
                {
                    ChunkedTransfer = true;
                }
                else if (key.Equals("x-amz-content-sha256") && val.ToLower().Contains("streaming"))
                {
                    ChunkedTransfer = true;
                }

                Headers.Add(key, val);
            }
        }

        internal async Task WriteHeaderAsync(Client client, bool sendToProxy = false)
        {
            var sb = new StringBuilder();

            string url = Uri.PathAndQuery;
            if (Method == HttpMethod.Connect) url = Uri.Authority;
            else if (Uri.Scheme == "http" && sendToProxy) url = Uri.ToString();

            sb.Append($"{Method} {url} {Version}\r\n");

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

        internal HttpRequest Clone()
        {
            var request = new HttpRequest(proxy);
            request.Method = Method;
            request.Uri = Uri;
            request.Version = Version;
            request.ChunkedTransfer = ChunkedTransfer;
            request.Body = Body;

            foreach (var header in Headers.GetAll())
            {
                foreach (var value in header.Value)
                {
                    request.Headers.Add(header.Key, value);
                }
            }

            return request;
        }
    }
}
