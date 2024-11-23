using Microsoft.AspNetCore.SignalR;
using ChatterBox.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using ChatterBox.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatterBox.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private static Dictionary<string, string> _userConnectionMap = new Dictionary<string, string>();

        public ChatHub(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
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

                // Load user's contacts and notify them
                var contacts = await _context.Contacts
                    .Where(c => c.ContactUserId == userId)
                    .Select(c => c.UserId)
                    .ToListAsync();

                foreach (var contactId in contacts)
                {
                    if (_userConnectionMap.ContainsKey(contactId))
                    {
                        await Clients.Client(_userConnectionMap[contactId])
                            .SendAsync("UserConnected", userId);
                    }
                }
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

                // Notify contacts about disconnection
                var contacts = await _context.Contacts
                    .Where(c => c.ContactUserId == userId)
                    .Select(c => c.UserId)
                    .ToListAsync();

                foreach (var contactId in contacts)
                {
                    if (_userConnectionMap.ContainsKey(contactId))
                    {
                        await Clients.Client(_userConnectionMap[contactId])
                            .SendAsync("UserDisconnected", userId);
                    }
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (senderId != null)
            {
                var sender = await _userManager.FindByIdAsync(senderId);

                // Save message to database
                var message = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsDeleted = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                var messageData = new
                {
                    MessageId = message.MessageId,
                    SenderId = senderId,
                    SenderName = sender?.UserName,
                    ReceiverId = receiverId,
                    Content = content,
                    Timestamp = message.SentAt,
                    IsRead = false
                };

                // Send to receiver if online
                if (_userConnectionMap.TryGetValue(receiverId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageData);
                }

                // Send back to sender for confirmation
                await Clients.Caller.SendAsync("MessageSent", messageData);
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

                    // Notify contacts about status update
                    var contacts = await _context.Contacts
                        .Where(c => c.ContactUserId == userId)
                        .Select(c => c.UserId)
                        .ToListAsync();

                    foreach (var contactId in contacts)
                    {
                        if (_userConnectionMap.ContainsKey(contactId))
                        {
                            await Clients.Client(_userConnectionMap[contactId])
                                .SendAsync("UserStatusUpdated", userId, status);
                        }
                    }
                }
            }
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId && m.ReceiverId == userId);

                if (message != null && !message.IsRead)
                {
                    message.IsRead = true;
                    await _context.SaveChangesAsync();

                    if (_userConnectionMap.TryGetValue(message.SenderId, out string connectionId))
                    {
                        await Clients.Client(connectionId)
                            .SendAsync("MessageRead", messageId);
                    }
                }
            }
        }
    }
}