using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class CommandSupport
{
    public static ConnectionOptions ReadConnection(InvocationContext ctx) => new(
        Host: ctx.ParseResult.GetValueForOption(CliOptions.Host)!,
        Port: ctx.ParseResult.GetValueForOption(CliOptions.Port),
        User: ctx.ParseResult.GetValueForOption(CliOptions.User)!,
        Pass: ctx.ParseResult.GetValueForOption(CliOptions.Pass)!,
        Timeout: TimeSpan.FromSeconds(ctx.ParseResult.GetValueForOption(CliOptions.Timeout)));

    public static OnvifClientProvider CreateProvider(ConnectionOptions c) =>
        new(new OnvifClientOptions
        {
            Timeout = c.Timeout,
            AllowSelfSignedCertificates = true
        });

    public static CameraDevice CreateCamera(ConnectionOptions c) => new()
    {
        IpAddress = c.Host,
        Port = c.Port,
        Endpoint = $"http://{c.Host}",
        Username = c.User,
        Password = c.Pass,
        IsManual = true
    };

    public static async Task<int> RunAsync(InvocationContext ctx, bool requireCredentials, Func<ConnectionOptions, CancellationToken, Task<int>> body)
    {
        var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
        var ct = ctx.GetCancellationToken();
        try
        {
            var conn = ReadConnection(ctx);
            if (requireCredentials && string.IsNullOrEmpty(conn.Pass))
            {
                OutputFormatter.WriteError("password is required: pass --pass or set ONVIF_PASSWORD env var", 2, json);
                return 2;
            }
            return await body(conn, ct);
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.Message;
            var isAuth = msg.Contains("401") || msg.Contains("403") ||
                         msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                         msg.Contains("Forbidden", StringComparison.OrdinalIgnoreCase);
            var code = isAuth ? 4 : 3;
            OutputFormatter.WriteError(msg, code, json);
            return code;
        }
        catch (OperationCanceledException)
        {
            OutputFormatter.WriteError("operation cancelled", 1, json);
            return 1;
        }
        catch (Exception ex)
        {
            OutputFormatter.WriteError($"{ex.GetType().Name}: {ex.Message}", 1, json);
            return 1;
        }
    }
}
