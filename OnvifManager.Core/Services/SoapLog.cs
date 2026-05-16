using System.IO;
using System.Net.Http;
using System.Text;

namespace OnvifManager.Services;

public static class SoapLog
{
    public static readonly string FilePath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "OnvifManager", "soap.log");
    private static readonly object Lock = new();

    static SoapLog()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath,
                $"=== ONVIF SOAP log started {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz} ===\n\n");
        }
        catch { }
    }

    public static void WriteRequest(string uri, string action, string body)
    {
        Write(sb =>
        {
            sb.AppendLine($"--- REQUEST {DateTime.Now:HH:mm:ss.fff} ---");
            sb.AppendLine($"POST {uri}");
            sb.AppendLine($"SOAPAction: {action}");
            sb.AppendLine(body);
            sb.AppendLine();
        });
    }

    public static void WriteResponse(string uri, HttpResponseMessage response, string body)
    {
        Write(sb =>
        {
            sb.AppendLine($"--- RESPONSE {DateTime.Now:HH:mm:ss.fff} ---");
            sb.AppendLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} from {uri}");
            foreach (var h in response.Headers)
                sb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
            foreach (var h in response.Content.Headers)
                sb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
            sb.AppendLine();
            sb.AppendLine(body);
            sb.AppendLine();
        });
    }

    public static void WriteNote(string note)
    {
        Write(sb =>
        {
            sb.AppendLine($"--- NOTE {DateTime.Now:HH:mm:ss.fff} ---");
            sb.AppendLine(note);
            sb.AppendLine();
        });
    }

    private static void Write(Action<StringBuilder> build)
    {
        try
        {
            var sb = new StringBuilder();
            build(sb);
            lock (Lock)
                File.AppendAllText(FilePath, sb.ToString());
        }
        catch { }
    }
}
