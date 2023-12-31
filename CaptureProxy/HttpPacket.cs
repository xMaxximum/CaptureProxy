using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    public abstract class HttpPacket
    {
        public bool ChunkedTransfer { get; set; } = false;
        public HeaderCollection Headers { get; } = new HeaderCollection();
        public byte[]? Body { get; protected set; }

        public abstract Task WriteHeaderAsync(Stream stream, CancellationToken token);

        public async Task ReadBodyAsync(Stream stream, CancellationToken token)
        {
            if (Headers.ContentLength > 0)
            {
                byte[] body = await Helper.StreamReadExactlyAsync(stream, Headers.ContentLength.Value, token).ConfigureAwait(false);
                SetBody(body);
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

                SetBody(ms.ToArray());
            }
        }

        public async Task WriteBodyAsync(Stream stream, CancellationToken token)
        {
            if (Body == null) return;
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

        public void SetBody(byte[] body)
        {
            Body = body;
            Headers.AddOrReplace("Content-Length", Body.Length.ToString());
            if (ChunkedTransfer)
            {
                Headers.Remove("Transfer-Encoding");
                ChunkedTransfer = false;
            }
        }

        public void SetBody(string body)
        {
            Encoding encoding = Encoding.UTF8;
            var contentType = Headers.GetAsFisrtValue("Content-Type");
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

            var contentEncoding = Headers.GetAsFisrtValue("Content-Encoding");
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
            Headers.AddOrReplace("Content-Length", Body.Length.ToString());
            if (Headers.GetAsFisrtValue("Transfer-Encoding") == "chunked")
            {
                Headers.Remove("Transfer-Encoding");
                ChunkedTransfer = false;
            }
        }

        private void DecodeGZipBody()
        {
            if (Body == null) return;

            using MemoryStream src = new MemoryStream(Body);
            using MemoryStream dest = new MemoryStream();
            using GZipStream stream = new GZipStream(src, CompressionMode.Decompress);
            stream.CopyTo(dest);

            Body = dest.ToArray();
        }
        private void DecodeDeflateBody()
        {
            if (Body == null) return;

            using MemoryStream src = new MemoryStream(Body);
            using MemoryStream dest = new MemoryStream();
            using DeflateStream stream = new DeflateStream(src, CompressionMode.Decompress);
            stream.CopyTo(dest);

            Body = dest.ToArray();
        }

        private void DecodeBrotliBody()
        {
            if (Body == null) return;

            using MemoryStream src = new MemoryStream(Body);
            using MemoryStream dest = new MemoryStream();
            using BrotliStream stream = new BrotliStream(src, CompressionMode.Decompress);
            stream.CopyTo(dest);

            Body = dest.ToArray();
        }
    }
}
