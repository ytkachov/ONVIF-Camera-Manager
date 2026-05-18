using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Cli.Store;

namespace OnvifManager.Cli.Commands;

internal static class CamerasListCommand
{
    public static Command Build()
    {
        var cmd = new Command("list", "List cameras stored by the WPF app (name, IP, port, user)");

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var cameras = CliCameraStore.LoadAll();
            var projected = cameras.Select(c => new
            {
                name = c.Name,
                ip = c.IpAddress,
                port = c.Port,
                user = c.Username,
                hasPassword = !string.IsNullOrEmpty(c.Password),
                manufacturer = c.Manufacturer,
                model = c.Model
            }).ToList();

            if (json)
            {
                OutputFormatter.Write(new { storePath = CliCameraStore.StorePath, cameras = projected }, json: true);
            }
            else if (projected.Count == 0)
            {
                OutputFormatter.Write($"no cameras stored at {CliCameraStore.StorePath}", json: false);
            }
            else
            {
                OutputFormatter.Write($"store: {CliCameraStore.StorePath}", json: false);
                OutputFormatter.Write(projected, json: false);
            }
            ctx.ExitCode = 0;
        });

        return cmd;
    }
}
