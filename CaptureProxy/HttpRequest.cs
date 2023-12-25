using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace CaptureProxy
{
    public class HttpRequest : IDisposable
    {
        private HttpRequestMessage _message = new HttpRequestMessage();
        private bool _chunkedTransfer = false;

        public HttpMethod Method => _message.Method;
        public Uri RequestUri
        {
            get
            {
                if (_message.RequestUri == null) throw new InvalidOperationException($"Failed to read RequestUri. Set it first.");
                return _message.RequestUri;
            }
        }
        public Version Version => _message.Version;
        public HttpRequestHeaders Headers => _message.Headers;
        public HttpContent? Body => _message.Content;

        public void Dispose()
        {
            _message.Dispose();
        }

        public async Task ReadHeaderAsync(Stream stream, string baseUrl, CancellationToken token)
        {
            // Process first Line
            string line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
            string[] lineSplit = line.Split(' ');
            if (lineSplit.Length < 3) throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");

            _message.Method = HttpMethod.Parse(lineSplit[0]);

            string url = lineSplit[1];
            if (_message.Method == HttpMethod.Connect)
            {
                url = "http://" + url;
            }
            else if (url.ToLower().StartsWith("http") == false)
            {
                if (string.IsNullOrEmpty(baseUrl)) throw new ArgumentException("Due url is relative, you must set baseUrl to complete the url.");
                url = baseUrl.TrimEnd('/') + url;
            }

            _message.RequestUri = new Uri(url);

            string[] versionSplit = lineSplit[2].Split('/');
            if (versionSplit.Length < 2) throw new ArgumentException("Request protocol/version does not contain at least two parts.");
            if (versionSplit[0].ToLower() != "http") throw new ArgumentException("Request protocol/version is not HTTP.");

            _message.Version = Version.Parse(versionSplit[1]);

            // Process subsequent Line
            while (!token.IsCancellationRequested)
            {
                line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line)) break;

                int splitOffet = line.IndexOf(':');
                if (splitOffet == -1) throw new ArgumentException("Request header does not contain at least two parts (key: value).");

                string key = line.Substring(0, splitOffet).Trim().ToLower();
                if (string.IsNullOrEmpty(key)) continue;

                string val = line.Substring(splitOffet + 1).Trim();

                if (key.Equals("transfer-encoding") && val.ToLower().Equals("chunked"))
                {
                    _chunkedTransfer = true;
                }
                else if (key.Equals("x-amz-content-sha256") && val.ToLower().Contains("streaming"))
                {
                    _chunkedTransfer = true;
                }

                _message.Headers.Add(key, val);
            }
        }

        public async Task WriteHeaderAsync(Stream stream, CancellationToken token)
        {
            if (_message.RequestUri == null) throw new InvalidOperationException($"Failed to read RequestUri. Set it first.");

            StringBuilder sb = new StringBuilder();

            sb.Append(_message.Method);
            sb.Append(' ');

            if (_message.Method == HttpMethod.Connect)
            {
                sb.Append($"{_message.RequestUri.Host}:{_message.RequestUri.Port}");
            }
            else
            {
                sb.Append(_message.RequestUri);
            }
            sb.Append(' ');

            sb.Append($"HTTP/{_message.Version}");
            sb.Append("\r\n");

            if (_message.Headers.Host == null)
            {
                _message.Headers.Host = _message.RequestUri.Host;
            }

            foreach (var item in _message.Headers)
            {
                foreach (var value in item.Value)
                {
                    sb.Append($"{item.Key}: {value}\r\n");
                }
            }

            if (_message.Content != null)
            {
                // Calling the 'getter' for the ContentLength property will reconcile the strings cache.
                Debug.WriteLine("ContentLength: " + _message.Content.Headers.ContentLength);

                foreach (var item in _message.Content.Headers)
                {
                    foreach (var value in item.Value)
                    {
                        sb.Append($"{item.Key}: {value}\r\n");
                    }
                }
            }

            sb.Append("\r\n");

            await stream.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()), token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
    }
}
