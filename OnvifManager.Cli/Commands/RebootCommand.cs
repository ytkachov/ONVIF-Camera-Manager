using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class RebootCommand
{
    private static readonly Option<bool> Yes = new(
        name: "--yes",
        description: "Confirm reboot without prompting (required)");

    public static Command Build()
    {
        var cmd = new Command("reboot", "Reboot the camera (SystemReboot, --yes required)");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(Yes);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var confirmed = ctx.ParseResult.GetValueForOption(Yes);
            if (!confirmed)
            {
                OutputFormatter.WriteError("reboot requires --yes to confirm", 2, json);
                ctx.ExitCode = 2;
                return;
            }

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                var msg = await device.RebootAsync(ct);
                if (json) OutputFormatter.Write(new { ok = true, message = msg }, json: true);
                else OutputFormatter.Write($"ok: {msg}", json: false);
                return 0;
            });
        });

        return cmd;
    }
}
