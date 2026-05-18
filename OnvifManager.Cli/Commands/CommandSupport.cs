using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using OnvifManager.Cli.Output;
using OnvifManager.Cli.Store;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class CommandSupport
{
    public static ConnectionOptions? TryReadConnection(InvocationContext ctx, bool requireCredentials, out string? error)
    {
        error = null;
        var pr = ctx.ParseResult;

        var hostInput = pr.GetValueForOption(CliOptions.Host);
        var port = pr.GetValueForOption(CliOptions.Port);
        var portExplicit = !IsImplicit(pr, CliOptions.Port);
        var user = pr.GetValueForOption(CliOptions.User);
        var pass = pr.GetValueForOption(CliOptions.Pass);
        var timeout = TimeSpan.FromSeconds(pr.GetValueForOption(CliOptions.Timeout));

        if (string.IsNullOrWhiteSpace(hostInput))
        {
            error = "--host is required (IP, hostname, or stored camera name)";
            return null;
        }

        var stored = CliCameraStore.TryResolve(hostInput);
        var host = stored?.IpAddress ?? hostInput;

        if (stored != null)
        {
            if (!portExplicit && stored.Port > 0) port = stored.Port;
            if (string.IsNullOrEmpty(user)) user = stored.Username;
            if (string.IsNullOrEmpty(pass)) pass = stored.Password;
        }

        if (string.IsNullOrEmpty(pass))
            pass = Environment.GetEnvironmentVariable("ONVIF_PASSWORD");

        if (requireCredentials)
        {
            if (string.IsNullOrEmpty(user))
            {
                error = "--user is required (or use --host <stored-camera-name>)";
                return null;
            }
            if (string.IsNullOrEmpty(pass))
            {
                error = "password is required: pass --pass, set ONVIF_PASSWORD, or use --host <stored-camera-name>";
                return null;
            }
        }

        return new ConnectionOptions(host, port, user ?? string.Empty, pass ?? string.Empty, timeout);
    }

    private static bool IsImplicit<T>(ParseResult pr, Option<T> opt)
    {
        var result = pr.FindResultFor(opt);
        return result?.IsImplicit ?? true;
    }

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
            var conn = TryReadConnection(ctx, requireCredentials, out var err);
            if (conn == null)
            {
                OutputFormatter.WriteError(err ?? "invalid arguments", 2, json);
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
