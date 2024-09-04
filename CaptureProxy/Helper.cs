using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace CaptureProxy
{
    internal class Helper
    {
        public static T DeepClone<T>(T obj)
        {
            var json = JsonSerializer.Serialize(obj);
#pragma warning disable CS8603 // Possible null reference return.
            return JsonSerializer.Deserialize<T>(json);
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
