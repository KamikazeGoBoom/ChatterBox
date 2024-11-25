using ChatterBox.Models;

namespace ChatterBox.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string userId, string title, string message, string type, string relatedEntityId = null);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(string userId);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 20);
        Task DeleteNotificationAsync(int notificationId);
        Task DeleteAllNotificationsAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<int> GetTotalCountAsync(string userId);
    }
}