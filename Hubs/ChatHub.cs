﻿using Microsoft.AspNetCore.SignalR;
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
            await base.OnDisconnectedAsync(exception);
        }

        // Direct Message Methods
        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (senderId != null)
            {
                var sender = await _userManager.FindByIdAsync(senderId);

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

                if (_userConnectionMap.TryGetValue(receiverId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageData);
                }

                await Clients.Caller.SendAsync("MessageSent", messageData);
            }
        }

        // Group Message Methods
        public async Task JoinGroup(int groupId)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            // Verify user is a member of the group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (isMember)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
            }
        }

        public async Task LeaveGroup(int groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
        }

        public async Task SendGroupMessage(int groupId, string content)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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

            await Clients.Group(groupId.ToString()).SendAsync("ReceiveGroupMessage", new
            {
                MessageId = message.MessageId,
                SenderId = userId,
                SenderName = sender.UserName,
                GroupId = groupId,
                Content = content,
                SentAt = message.SentAt
            });
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

        public async Task MarkGroupMessageAsRead(int messageId)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
                }
            }
        }
    }
}