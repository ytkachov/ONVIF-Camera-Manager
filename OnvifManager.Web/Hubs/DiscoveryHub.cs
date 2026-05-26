using Microsoft.AspNetCore.SignalR;

namespace OnvifManager.Web.Hubs;

// Client must Join the per-session group BEFORE POSTing /api/discovery/start;
// otherwise the first DeviceFound events may broadcast before the connection
// is in the group and would be dropped. M2 accepts this UX constraint; a more
// robust "subscribe + start" hand-off can ship later if it bites.
public sealed class DiscoveryHub : Hub
{
    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTime.UtcNow);

    public Task JoinSession(string sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public Task LeaveSession(string sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    public static string GroupName(string sessionId) => $"discovery:{sessionId}";
}
