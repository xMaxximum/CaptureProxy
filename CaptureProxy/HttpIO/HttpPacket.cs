using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace CaptureProxy.HttpIO
{
    public class HttpPacket(HttpProxy proxy)
    {
        protected readonly HttpProxy proxy = proxy;

        internal bool ChunkedTransfer { get; set; } = false;

        public HeaderCollection Headers { get; } = new HeaderCollection();
        public byte[]? Body { get; protected set; }

        internal void Dispose()
        {
            Body = null;
            Headers.Dispose();
        }

        internal async Task ReadBodyAsync(Client client)
        {
            if (Headers.ContentLength > 0)
            {
                byte[] body = await client.ReadExtractAsync(Headers.ContentLength).ConfigureAwait(false);
                SetBody(body);
            }
            else if (ChunkedTransfer)
            {
                var ms = new MemoryStream();

                while (true)
                {
                    if (proxy.Token.IsCancellationRequested) break;

                    byte[] buffer = await ReadChunkAsync(client).ConfigureAwait(false);
                    if (buffer.Length == 0) break;

                    ms.Write(buffer);
                }

                SetBody(ms.ToArray());
            }
        }

        internal async Task WriteBodyAsync(Client client)
        {
            if (Body == null) return;
            await client.WriteAsync(Body, proxy.Token).ConfigureAwait(false);
            await client.Stream.FlushAsync(proxy.Token).ConfigureAwait(false);
        }

        internal async Task<byte[]> ReadChunkAsync(Client client)
        {
            string hexLength = await client.ReadLineAsync(proxy.Settings.MaxChunkSizeLine).ConfigureAwait(false);
            if (int.TryParse(hexLength, NumberStyles.HexNumber, null, out int chunkSize) == false)
            {
                throw new InvalidOperationException($"Chunk size {hexLength} is not valid.");
            }

            byte[] buffer = await client.ReadExtractAsync(chunkSize).ConfigureAwait(false);
            string endOfChunk = await client.ReadLineAsync(2).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(endOfChunk))
            {
                throw new InvalidOperationException($"End of chunk {hexLength} is not CRLF bytes.");
            }

            return buffer;
        }

        internal async Task WriteChunkAsync(Client client, byte[] buffer)
        {
            string hexLength = buffer.Length.ToString("X").ToLower();
            await client.WriteAsync(Encoding.UTF8.GetBytes(hexLength + "\r\n"), proxy.Token).ConfigureAwait(false);
            await client.WriteAsync(buffer, proxy.Token).ConfigureAwait(false);
            await client.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), proxy.Token).ConfigureAwait(false);
            await client.Stream.FlushAsync(proxy.Token).ConfigureAwait(false);
        }

        public void SetBody(byte[] body)
        {
            Body = body;
            Headers.ContentLength = Body.Length;
            if (ChunkedTransfer)
            {
                Headers.Remove("Transfer-Encoding");
                ChunkedTransfer = false;
            }
        }

        public void SetBody(string body)
        {
            Encoding encoding = Encoding.UTF8;
            var contentType = Headers.GetFirstValue("Content-Type");
            if (contentType != null)
            {
                var charsetIndex = contentType.ToLower().IndexOf("charset=");
                if (charsetIndex != -1)
                {
                    string encodingName = contentType.Substring(charsetIndex + 8);
                    try
                    {
                        encoding = Encoding.GetEncoding(encodingName);
                    }
                    catch { }
                }
            }

            SetBody(encoding.GetBytes(body));
        }

        public void SetBody(string body, string mediaType, Encoding encoding)
        {
            Headers.AddOrReplace("Content-Type", $"{mediaType}; charset={encoding.WebName}");
            SetBody(encoding.GetBytes(body));
        }

        public void DecodeBody()
        {
            if (Body == null) return;

            var contentEncoding = Headers.GetFirstValue("Content-Encoding");
            if (contentEncoding == null) return;

            switch (contentEncoding.ToLower())
            {
                case "gzip":
                    DecodeGZipBody();
                    break;

                case "compress":
                case "deflate":
                    DecodeDeflateBody();
                    break;

                case "br":
                    DecodeBrotliBody();
                    break;

                default:
                    throw new NotSupportedException($"Content encoding {contentEncoding} is not supported.");
            }

            Headers.Remove("Content-Encoding");

            if (Headers.ContentLength > 0)
            {
                Headers.ContentLength = Body.Length;
            }
        }

        protected void DecodeGZipBody()
        {
            if (Body == null) return;

            using MemoryStream src = new MemoryStream(Body);
            using MemoryStream dest = new MemoryStream();
            using GZipStream stream = new GZipStream(src, CompressionMode.Decompress);
            stream.CopyTo(dest);

            Body = dest.ToArray();
        }

        protected void DecodeDeflateBody()
        {
            if (Body == null) return;

            using MemoryStream src = new MemoryStream(Body);
            using MemoryStream dest = new MemoryStream();
            using DeflateStream stream = new DeflateStream(src, CompressionMode.Decompress);
            stream.CopyTo(dest);

            Body = dest.ToArray();
        }

        protected void DecodeBrotliBody()
        {
            if (Body == null) return;

            using MemoryStream src = new MemoryStream(Body);
            using MemoryStream dest = new MemoryStream();
            using BrotliStream stream = new BrotliStream(src, CompressionMode.Decompress);
            stream.CopyTo(dest);

            Body = dest.ToArray();
        }

        internal async Task TransferBodyAsync(Client client, Client remote)
        {
            if (Headers.ContentLength > 0)
            {
                int bytesRead = 0;
                var buffer = new Memory<byte>(new byte[proxy.Settings.StreamBufferSize]);

                long remaining = Headers.ContentLength;
                while (true)
                {
                    if (remaining <= 0) break;
                    if (proxy.Token.IsCancellationRequested) break;

                    bytesRead = await client.ReadAsync(buffer).ConfigureAwait(false);
                    remaining -= bytesRead;

                    await remote.Stream.WriteAsync(buffer[..bytesRead], proxy.Token).ConfigureAwait(false);
                }
            }
            else if (ChunkedTransfer)
            {
                while (true)
                {
                    if (proxy.Token.IsCancellationRequested) break;

                    var buffer = await ReadChunkAsync(client).ConfigureAwait(false);
                    await WriteChunkAsync(remote, buffer).ConfigureAwait(false);

                    if (buffer.Length == 0) break;
                }
            }
        }
    }
}
