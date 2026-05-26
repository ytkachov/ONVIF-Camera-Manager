using Microsoft.AspNetCore.SignalR;

namespace OnvifManager.Web.Hubs;

// The client generates the sessionId, invokes JoinSession(sessionId) on this
// hub, and only then POSTs /api/discovery/start { sessionId } — guaranteeing
// the group is populated before any DeviceFound events can broadcast.
public sealed class DiscoveryHub : Hub
{
    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTime.UtcNow);

    public Task JoinSession(string sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public Task LeaveSession(string sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public static string GroupName(string sessionId) => $"discovery:{sessionId}";
}
