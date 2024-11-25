using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatterBox.Hubs
{
    public class NotificationHub : Hub
    {
        private static readonly Dictionary<string, string> _userConnections =
            new Dictionary<string, string>();

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                _userConnections[userId] = Context.ConnectionId;
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && _userConnections.ContainsKey(userId))
            {
                _userConnections.Remove(userId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}