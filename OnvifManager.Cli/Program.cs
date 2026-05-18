using System.CommandLine;
using OnvifManager.Cli;
using OnvifManager.Cli.Commands;

var root = new RootCommand(
    "ONVIF camera manager CLI — read and write camera parameters via command-line options");

root.AddGlobalOption(CliOptions.Json);

var get = new Command("get", "Read parameters from a camera");
get.AddCommand(DeviceInfoGetCommand.Build());
get.AddCommand(HostnameGetCommand.Build());
get.AddCommand(DeviceNameGetCommand.Build());
get.AddCommand(ScopesGetCommand.Build());
get.AddCommand(ServicesGetCommand.Build());
get.AddCommand(NetworkGetCommand.Build());
get.AddCommand(DnsGetCommand.Build());
get.AddCommand(NtpGetCommand.Build());
get.AddCommand(TimeGetCommand.Build());
get.AddCommand(ProfilesGetCommand.Build());
get.AddCommand(EncoderGetCommand.Build());
get.AddCommand(StreamUriGetCommand.Build());
get.AddCommand(SnapshotUriGetCommand.Build());
get.AddCommand(ImagingGetCommand.Build());

var set = new Command("set", "Write parameters to a camera");
set.AddCommand(HostnameSetCommand.Build());
set.AddCommand(DeviceNameSetCommand.Build());
set.AddCommand(NetworkSetCommand.Build());
set.AddCommand(EncoderSetCommand.Build());
set.AddCommand(ImagingSetCommand.Build());

var device = new Command("device", "Device-level actions");
device.AddCommand(RebootCommand.Build());

root.AddCommand(DiscoverCommand.Build());
root.AddCommand(get);
root.AddCommand(set);
root.AddCommand(device);
root.AddCommand(SnapshotCaptureCommand.Build());

return await root.InvokeAsync(args);
