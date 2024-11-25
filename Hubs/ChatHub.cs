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

        public ChatHub(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
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
                    user.LastSeen = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

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

                            // Notify contacts that user is online
                            await _notificationService.CreateNotificationAsync(
                                contactId,
                                "Contact Online",
                                $"{user.UserName} is now online",
                                "UserStatus"
                            );
                        }
                    }

                    // Join all groups the user is a member of
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
                    user.LastSeen = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Notify contacts that user went offline
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

                            // Create offline notification
                            await _notificationService.CreateNotificationAsync(
                                contactId,
                                "Contact Offline",
                                $"{user.UserName} has gone offline",
                                "UserStatus"
                            );
                        }
                    }

                    // Leave all groups
                    var userGroups = await _context.GroupMembers
                        .Where(gm => gm.UserId == userId)
                        .Select(gm => gm.GroupId)
                        .ToListAsync();

                    foreach (var groupId in userGroups)
                    {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
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
                    SenderName = sender.UserName,
                    ReceiverId = receiverId,
                    Content = content,
                    Timestamp = message.SentAt,
                    IsRead = false
                };

                // Send real-time message
                if (_userConnectionMap.TryGetValue(receiverId, out string? connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageData);
                }

                await Clients.Caller.SendAsync("MessageSent", messageData);

                // Create notification for receiver
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

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return;

            // Verify user is a member of the group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (isMember)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());

                // Notify group members
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
                            $"Group Activity",
                            $"{user.UserName} joined {group.Name}",
                            "GroupActivity",
                            groupId.ToString()
                        );
                    }
                }
            }
        }

        public async Task LeaveGroup(int groupId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());

            // Notify group members
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
                        $"Group Activity",
                        $"{user.UserName} left {group.Name}",
                        "GroupActivity",
                        groupId.ToString()
                    );
                }
            }
        }

        public async Task SendGroupMessage(int groupId, string content)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            var sender = await _userManager.FindByIdAsync(userId);
            if (sender == null) return;

            // Verify user is a member of the group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (!isMember) return;

            var message = new Message
            {
                SenderId = userId,
                GroupId = groupId,
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
                SenderId = userId,
                SenderName = sender.UserName,
                GroupId = groupId,
                Content = content,
                SentAt = message.SentAt
            };

            await Clients.Group(groupId.ToString()).SendAsync("ReceiveGroupMessage", messageData);

            // Create notifications for group members
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

        public async Task UpdateStatus(string status)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    var oldStatus = user.Status;
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

                            // Notify contacts of status change
                            if (oldStatus != status)
                            {
                                await _notificationService.CreateNotificationAsync(
                                    contactId,
                                    "Contact Status Update",
                                    $"{user.UserName} is now {status}",
                                    "UserStatus"
                                );
                            }
                        }
                    }
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

                        // Notify sender that message was read
                        var reader = await _userManager.FindByIdAsync(userId);
                        if (reader != null)
                        {
                            await _notificationService.CreateNotificationAsync(
                                message.SenderId,
                                "Message Read",
                                $"{reader.UserName} has read your message",
                                "MessageStatus",
                                messageId.ToString()
                            );
                        }
                    }
                }
            }
        }

        public async Task MarkGroupMessageAsRead(int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (message != null && !message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();

                var groupId = message.GroupId;
                if (groupId.HasValue)
                {
                    await Clients.Group(groupId.ToString())
                        .SendAsync("GroupMessageRead", messageId, userId);

                    // Notify sender that message was read
                    var reader = await _userManager.FindByIdAsync(userId);
                    if (reader != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            message.SenderId,
                            "Group Message Read",
                            $"{reader.UserName} has read your message in the group",
                            "MessageStatus",
                            messageId.ToString()
                        );
                    }
                }
            }
        }
    }
}