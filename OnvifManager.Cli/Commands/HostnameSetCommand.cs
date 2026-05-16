using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class HostnameSetCommand
{
    public static Command Build()
    {
        var cmd = new Command("hostname", "Write ONVIF Hostname value");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.HostnameValue);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                var value = ctx.ParseResult.GetValueForOption(CliOptions.HostnameValue)!;
                using var provider = CommandSupport.CreateProvider(conn);
                var client = provider.Get(CommandSupport.CreateCamera(conn));
                var device = new DeviceService(client);
                await device.SetHostnameAsync(value, ct);

                if (json)
                    OutputFormatter.Write(new { ok = true, hostname = value }, json: true);
                else
                    OutputFormatter.Write($"ok: hostname set to {value}", json: false);
                return 0;
            });
        });

        return cmd;
    }
}
