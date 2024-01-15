using System;
using System.Diagnostics;
using System.Text;

namespace CaptureProxy
{
    public class HttpRequest : HttpPacket, IDisposable
    {
        private Uri? _uri;

        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public Uri RequestUri
        {
            get
            {
                if (_uri == null) throw new InvalidOperationException($"Failed to read RequestUri. Set it first.");
                return _uri;
            }
            set { _uri = value; }
        }
        public string Version { get; set; } = "HTTP/1.1";

        public void Dispose()
        {
            Body = null;
            Headers.Dispose();
        }

        public async Task ReadHeaderAsync(Stream stream, string baseUrl, CancellationToken token)
        {
            // Process first Line
            string line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
            string[] lineSplit = line.Split(' ');
            if (lineSplit.Length < 3)
            {
                throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");
            }

            Method = HttpMethod.Parse(lineSplit[0]);

            string url = lineSplit[1];
            if (Method == HttpMethod.Connect)
            {
                url = "http://" + url;
            }
            else if (url.ToLower().StartsWith("http") == false)
            {
                if (string.IsNullOrEmpty(baseUrl))
                {
                    throw new ArgumentException("Due url is relative, you must set baseUrl to complete the url.");
                }
                url = baseUrl.TrimEnd('/') + url;
            }

            RequestUri = new Uri(url);

            Version = lineSplit[2];

            // Process subsequent Line
            while (!token.IsCancellationRequested)
            {
                line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
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

        public override async Task WriteHeaderAsync(Stream stream, CancellationToken token)
        {
            if (RequestUri == null) throw new InvalidOperationException($"Failed to read RequestUri. Set it first.");

            StringBuilder sb = new StringBuilder();

            sb.Append(Method);
            sb.Append(' ');

            if (Method == HttpMethod.Connect)
            {
                sb.Append($"{RequestUri.Host}:{RequestUri.Port}");
            }
            else if (RequestUri.Scheme == "https")
            {
                sb.Append(RequestUri.PathAndQuery);
            }
            else
            {
                sb.Append(RequestUri);
            }
            sb.Append(' ');

            sb.Append(Version);
            sb.Append("\r\n");

            if (Headers.GetAsFisrtValue("Host") == null)
            {
                Headers.AddOrReplace("Host", RequestUri.Host);
            }

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
    }
}
