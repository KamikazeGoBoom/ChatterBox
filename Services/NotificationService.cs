using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ChatterBox.Data;
using ChatterBox.Hubs;
using ChatterBox.Models;

namespace ChatterBox.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _notificationHub = notificationHub;
        }

        public async Task CreateNotificationAsync(string userId, string title, string message, string type, string? relatedEntityId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            await _notificationHub.Clients.User(userId)
                .SendAsync("ReceiveNotification", new
                {
                    notification.NotificationId,
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.CreatedAt,
                    notification.IsRead
                });
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 20)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task DeleteNotificationAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAllNotificationsAsync(string userId)
        {
            await _context.Notifications
                .Where(n => n.UserId == userId)
                .ExecuteDeleteAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }

        public async Task<int> GetTotalCountAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .CountAsync();
        }
    }
}