using System;
using System.Diagnostics;
using System.Text;

namespace CaptureProxy.HttpIO
{
    public class HttpRequest : HttpPacket
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;

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

        public async Task ReadHeaderAsync(Client client, Uri? baseUri = null)
        {
            // Process first Line
            string line = await client.ReadLineAsync(Settings.MaxIncomingHeaderLine);
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
            while (Settings.ProxyIsRunning)
            {
                line = await client.ReadLineAsync(Settings.MaxIncomingHeaderLine);
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

        public override async Task WriteHeaderAsync(Client client)
        {
            StringBuilder sb = new StringBuilder();

            string url = Method == HttpMethod.Connect ? Uri.Authority : Uri.ToString();
            sb.Append($"{Method} {url} {Version}\r\n");

            foreach (var item in Headers.GetAll())
            {
                foreach (var value in item.Value)
                {
                    sb.Append($"{item.Key}: {value}\r\n");
                }
            }

            sb.Append("\r\n");

            await client.Stream.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()));
            await client.Stream.FlushAsync();
        }
    }
}
