using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace CaptureProxy
{
    public class HttpResponse : IDisposable
    {
        private HttpResponseMessage _message = new HttpResponseMessage();
        private bool _chunkedTransfer = false;

        public Version Version
        {
            get => _message.Version;
            set => _message.Version = value;
        }
        public HttpStatusCode StatusCode
        {
            get => _message.StatusCode;
            set => _message.StatusCode = value;
        }
        public string? ReasonPhrase
        {
            get => _message.ReasonPhrase;
            set => _message.ReasonPhrase = value;
        }
        public HttpResponseHeaders Headers => _message.Headers;
        public HttpContent Body => _message.Content;

        public void Dispose()
        {
            _message.Dispose();
        }

        public async Task ReadHeaderAsync(Stream stream, CancellationToken token)
        {
            // Process first Line
            string line = await Helper.StreamReadLineAsync(stream, Settings.MaxIncomingHeaderLine, token).ConfigureAwait(false);
            string[] lineSplit = line.Split(' ');
            if (lineSplit.Length < 2) throw new ArgumentException("Response line does not contain at least two parts (version, status, [reason pharse]).");

            string[] versionSplit = lineSplit[0].Split('/');
            if (versionSplit.Length < 2) throw new ArgumentException("Response protocol/version does not contain at least two parts.");
            if (versionSplit[0].ToLower() != "http") throw new ArgumentException("Response protocol/version is not HTTP.");
            _message.Version = Version.Parse(versionSplit[1]);

            if (Enum.TryParse<HttpStatusCode>(lineSplit[1], out var statusCode) == false)
            {
                throw new ArgumentException("Response status code is not valid.");
            }
            _message.StatusCode = statusCode;

            StringBuilder sb = new StringBuilder();
            for (int i = 2; i < lineSplit.Length; i++)
            {
                sb.Append(lineSplit[i] + " ");
            }
            if (sb.Length > 0)
            {
                _message.ReasonPhrase = sb.ToString().Trim();
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

                if (_message.Content.Headers.Contains(key))
                {
                    _message.Content.Headers.Add(key, val);
                }
                else
                {
                    _message.Headers.Add(key, val);
                }
            }
        }

        public async Task WriteHeaderAsync(Stream stream, CancellationToken token)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"HTTP/{_message.Version} {Convert.ToInt16(_message.StatusCode)}");
            if (_message.ReasonPhrase != null)
            {
                sb.Append($" {_message.ReasonPhrase}");
            }
            sb.Append("\r\n");

            foreach (var item in _message.Headers)
            {
                foreach (var value in item.Value)
                {
                    sb.Append($"{item.Key}: {value}\r\n");
                }
            }

            // Calling the 'getter' for the ContentLength property will reconcile the strings cache.
            Debug.WriteLine("ContentLength: " + _message.Content.Headers.ContentLength);

            foreach (var item in _message.Content.Headers)
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
            _message.Content = new StringContent(body);
        }

        public void SetBody(string body, string mediaType, Encoding encoding)
        {
            _message.Content = new StringContent(body, encoding, mediaType);
        }

        public async Task WriteBodyAsync(Stream stream, CancellationToken token)
        {
            await _message.Content.CopyToAsync(stream, token).ConfigureAwait(false);
        }
    }
}
