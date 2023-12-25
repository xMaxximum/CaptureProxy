using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    public class HeaderCollection : IDisposable
    {
        private Dictionary<string, List<string>> Headers { get; } = new Dictionary<string, List<string>>();

        public long? ContentLength
        {
            get
            {
                var tmp = GetAsFisrtValue("Content-Length");
                if (tmp == null) return null;

                if (long.TryParse(tmp, out var length) == false) return null;
                return length;
            }
            set
            {
                if (value == null) return;
                AddOrReplace("Content-Length", value.Value.ToString());
            }
        }

        public void Dispose()
        {
            foreach (var item in Headers) item.Value.Clear();
            Headers.Clear();
        }

        public void Add(string key, string value)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false)
            {
                Headers[key] = new List<string>();
            }

            Headers[key].Add(value);
        }

        public void AddOrReplace(string key, string value)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false)
            {
                Headers[key] = new List<string>();
            }

            Headers[key].Clear();
            Headers[key].Add(value);
        }

        public void Remove(string key)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false) return;

            Headers[key].Clear();
            Headers.Remove(key);
        }

        public Dictionary<string, List<string>> GetAll()
        {
            return Headers;
        }

        public List<string> GetAsList(string key)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false) return new List<string>();

            return Headers[key];
        }

        public string? GetAsFisrtValue(string key)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false) return null;

            return Headers[key].FirstOrDefault();
        }
    }
}
