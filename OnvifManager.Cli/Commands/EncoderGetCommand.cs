using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class EncoderGetCommand
{
    private static readonly Option<string?> TokenFilter = new(
        name: "--token",
        description: "Filter by video encoder configuration token");

    public static Command Build()
    {
        var cmd = new Command("encoder", "List video encoder configurations");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(TokenFilter);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var tokenFilter = ctx.ParseResult.GetValueForOption(TokenFilter);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var media = new MediaService(provider.Get(CommandSupport.CreateCamera(conn)));
                var configs = await media.GetAllVideoEncoderConfigurationsAsync(ct);
                if (!string.IsNullOrEmpty(tokenFilter))
                    configs = configs.Where(c => c.Token == tokenFilter).ToList();
                OutputFormatter.Write(configs, json);
                return 0;
            });
        });

        return cmd;
    }
}
