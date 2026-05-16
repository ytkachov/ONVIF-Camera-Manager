using System.CommandLine;
using OnvifManager.Cli;
using OnvifManager.Cli.Commands;

var root = new RootCommand(
    "ONVIF camera manager CLI — read and write camera parameters via command-line options");

root.AddGlobalOption(CliOptions.Json);

var get = new Command("get", "Read parameters from a camera");
get.AddCommand(DeviceInfoGetCommand.Build());
get.AddCommand(HostnameGetCommand.Build());

var set = new Command("set", "Write parameters to a camera");
set.AddCommand(HostnameSetCommand.Build());

root.AddCommand(DiscoverCommand.Build());
root.AddCommand(get);
root.AddCommand(set);

return await root.InvokeAsync(args);
