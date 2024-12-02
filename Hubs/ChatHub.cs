using Microsoft.AspNetCore.SignalR;
using ChatterBox.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using ChatterBox.Data;
using ChatterBox.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatterBox.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private static Dictionary<string, string> _userConnectionMap = new Dictionary<string, string>();
        private static readonly TimeZoneInfo _philippinesTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        public ChatHub(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }

        private DateTime GetPhilippinesTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _philippinesTimeZone);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                _userConnectionMap[userId] = Context.ConnectionId;
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.Status = "Online";
                    user.LastSeen = GetPhilippinesTime();
                    await _userManager.UpdateAsync(user);

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

                    // Automatically join all groups the user is a member of
                    var userGroups = await _context.GroupMembers
                        .Where(gm => gm.UserId == userId)
                        .Select(gm => gm.GroupId)
                        .ToListAsync();

                    foreach (var groupId in userGroups)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
                    }
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                _userConnectionMap.Remove(userId);
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.Status = "Offline";
                    user.LastSeen = GetPhilippinesTime();
                    await _userManager.UpdateAsync(user);

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
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (senderId != null)
            {
                var sender = await _userManager.FindByIdAsync(senderId);
                if (sender == null) return;

                var message = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Content = content,
                    SentAt = GetPhilippinesTime(),
                    IsRead = false,
                    IsDeleted = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                var messageData = new
                {
                    MessageId = message.MessageId,
                    SenderId = senderId,
                    SenderName = sender.UserName,
                    ReceiverId = receiverId,
                    Content = content,
                    SentAt = message.SentAt,
                    IsRead = false
                };

                if (_userConnectionMap.TryGetValue(receiverId, out string? connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageData);
                }

                await Clients.Caller.SendAsync("MessageSent", messageData);

                await _notificationService.CreateNotificationAsync(
                    receiverId,
                    $"New message from {sender.UserName}",
                    content.Length > 50 ? content.Substring(0, 47) + "..." : content,
                    "DirectMessage",
                    senderId
                );
            }
        }

        public async Task JoinGroup(int groupId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (isMember)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    await Clients.Group(groupId.ToString()).SendAsync("UserJoinedGroup", groupId, user.UserName);
                }
            }
        }

        public async Task SendGroupMessage(int groupId, string content)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            var sender = await _userManager.FindByIdAsync(userId);
            if (sender == null) return;

            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (!isMember) return;

            var message = new Message
            {
                SenderId = userId,
                GroupId = groupId,
                Content = content,
                SentAt = GetPhilippinesTime(),
                IsRead = false,
                IsDeleted = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var messageData = new
            {
                MessageId = message.MessageId,
                SenderId = userId,
                SenderName = sender.UserName,
                GroupId = groupId,
                Content = content,
                SentAt = message.SentAt
            };

            await Clients.Group(groupId.ToString()).SendAsync("ReceiveGroupMessage", messageData);

            var group = await _context.Groups.FindAsync(groupId);
            if (group != null)
            {
                var members = await _context.GroupMembers
                    .Where(gm => gm.GroupId == groupId && gm.UserId != userId)
                    .Select(gm => gm.UserId)
                    .ToListAsync();

                foreach (var memberId in members)
                {
                    await _notificationService.CreateNotificationAsync(
                        memberId,
                        $"New message in {group.Name}",
                        $"{sender.UserName}: {(content.Length > 50 ? content.Substring(0, 47) + "..." : content)}",
                        "GroupMessage",
                        groupId.ToString()
                    );
                }
            }
        }

        public async Task LeaveGroup(int groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    await Clients.Group(groupId.ToString()).SendAsync("UserLeftGroup", groupId, user.UserName);
                }
            }
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId && m.ReceiverId == userId);

                if (message != null && !message.IsRead)
                {
                    message.IsRead = true;
                    await _context.SaveChangesAsync();

                    if (_userConnectionMap.TryGetValue(message.SenderId, out string? connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("MessageRead", messageId);
                    }
                }
            }
        }

        public async Task UpdateStatus(string status)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.Status = status;
                    await _userManager.UpdateAsync(user);

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
    }
}