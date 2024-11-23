using Microsoft.AspNetCore.SignalR;
using ChatterBox.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ChatterBox.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private static Dictionary<string, string> _userConnectionMap = new Dictionary<string, string>();

        public ChatHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                _userConnectionMap[userId] = Context.ConnectionId;
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.Status = "Online";
                    user.LastSeen = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }

                await Clients.Others.SendAsync("UserConnected", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                _userConnectionMap.Remove(userId);
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.Status = "Offline";
                    user.LastSeen = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }

                await Clients.Others.SendAsync("UserDisconnected", userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (senderId != null)
            {
                var sender = await _userManager.FindByIdAsync(senderId);
                var message = new
                {
                    SenderId = senderId,
                    SenderName = sender?.UserName,
                    ReceiverId = receiverId,
                    Content = content,
                    Timestamp = DateTime.UtcNow
                };

                if (_userConnectionMap.TryGetValue(receiverId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", message);
                }
                await Clients.Caller.SendAsync("MessageSent", message);
            }
        }

        public async Task UpdateStatus(string status)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.Status = status;
                    await _userManager.UpdateAsync(user);
                    await Clients.Others.SendAsync("UserStatusUpdated", userId, status);
                }
            }
        }
    }
}