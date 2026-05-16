using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class DiscoverCommand
{
    public static Command Build()
    {
        var cmd = new Command("discover", "WS-Discovery multicast probe for ONVIF cameras on the local network");
        cmd.AddOption(CliOptions.Timeout);
        cmd.AddOption(CliOptions.LocalIp);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var ct = ctx.GetCancellationToken();
            var timeoutSec = ctx.ParseResult.GetValueForOption(CliOptions.Timeout);
            var localIp = ctx.ParseResult.GetValueForOption(CliOptions.LocalIp);

            try
            {
                using var provider = new OnvifClientProvider(new OnvifClientOptions
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSec),
                    AllowSelfSignedCertificates = true
                });
                var service = new DiscoveryService(provider);
                var cameras = await service.DiscoverAsync(localIp, ct);

                var rows = cameras.Select(c => new
                {
                    c.IpAddress,
                    c.Port,
                    c.Endpoint,
                    c.Name,
                    c.HardwareId
                }).ToList();

                OutputFormatter.Write(rows, json);
                ctx.ExitCode = 0;
            }
            catch (OperationCanceledException)
            {
                OutputFormatter.WriteError("operation cancelled", 1, json);
                ctx.ExitCode = 1;
            }
            catch (Exception ex)
            {
                OutputFormatter.WriteError($"{ex.GetType().Name}: {ex.Message}", 1, json);
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }
}
