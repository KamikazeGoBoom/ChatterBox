using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatterBox.Models;
using ChatterBox.Data;
using ChatterBox.Services;

namespace ChatterBox.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;
        private const int PageSize = 10;

        public NotificationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(PageSize)
                .ToListAsync();

            return View(notifications);
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(int page = 1)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var skip = (page - 1) * PageSize;
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(PageSize)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Message,
                        n.Type,
                        n.RelatedEntityId,
                        n.IsRead,
                        n.CreatedAt
                    })
                    .ToListAsync();

                var unreadCount = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();

                var totalCount = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .CountAsync();

                return Json(new
                {
                    notifications,
                    unreadCount,
                    hasMore = (skip + notifications.Count) < totalCount
                });
            }
            catch
            {
                return Json(new { error = "Failed to load notifications" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == request.NotificationId && n.UserId == userId);

                if (notification == null)
                    return NotFound();

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ForEachAsync(n => n.IsRead = true);

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] DeleteNotificationRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == request.NotificationId && n.UserId == userId);

                if (notification == null)
                    return NotFound();

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAll()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var notifications = _context.Notifications.Where(n => n.UserId == userId);
                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var count = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();

                return Json(new { count });
            }
            catch (Exception ex)
            {
                return Json(new { count = 0, error = ex.Message });
            }
        }
    }

    public class MarkAsReadRequest
    {
        public int NotificationId { get; set; }
    }

    public class DeleteNotificationRequest
    {
        public int NotificationId { get; set; }
    }
}
