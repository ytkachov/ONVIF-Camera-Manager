using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class NetworkSetCommand
{
    public static Command Build()
    {
        var cmd = new Command("network", "Write network interface (merge specified fields into current config)");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.InterfaceToken);
        cmd.AddOption(CliOptions.NetDhcp);
        cmd.AddOption(CliOptions.NetIpv4);
        cmd.AddOption(CliOptions.NetPrefix);
        cmd.AddOption(CliOptions.NetGateway);
        cmd.AddOption(CliOptions.NetMtu);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var token = ctx.ParseResult.GetValueForOption(CliOptions.InterfaceToken)!;
            var dhcp = ctx.ParseResult.GetValueForOption(CliOptions.NetDhcp);
            var ipv4 = ctx.ParseResult.GetValueForOption(CliOptions.NetIpv4);
            var prefix = ctx.ParseResult.GetValueForOption(CliOptions.NetPrefix);
            var gateway = ctx.ParseResult.GetValueForOption(CliOptions.NetGateway);
            var mtu = ctx.ParseResult.GetValueForOption(CliOptions.NetMtu);

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                var interfaces = await device.GetNetworkInterfacesAsync(ct);
                var ni = interfaces.FirstOrDefault(i => i.Token == token);
                if (ni == null)
                {
                    OutputFormatter.WriteError($"network interface '{token}' not found (available: {string.Join(", ", interfaces.Select(i => i.Token))})", 1, json);
                    return 1;
                }

                if (dhcp.HasValue) ni.IPv4Dhcp = dhcp.Value;
                if (!string.IsNullOrEmpty(ipv4)) ni.IPv4Address = ipv4;
                if (prefix.HasValue) ni.IPv4PrefixLength = prefix.Value;
                if (!string.IsNullOrEmpty(gateway)) ni.IPv4Gateway = gateway;
                if (mtu.HasValue) ni.Mtu = mtu.Value;

                await device.SetNetworkInterfacesAsync(ni, ct);
                if (json) OutputFormatter.Write(new { ok = true, token = ni.Token, applied = ni }, json: true);
                else OutputFormatter.Write($"ok: network interface {ni.Token} updated", json: false);
                return 0;
            });
        });

        return cmd;
    }
}
