using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class ProfilesGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("profiles", "List media profiles");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var media = new MediaService(provider.Get(CommandSupport.CreateCamera(conn)));
                var profiles = await media.GetProfilesAsync(ct);
                OutputFormatter.Write(profiles, json);
                return 0;
            });
        });

        return cmd;
    }
}
