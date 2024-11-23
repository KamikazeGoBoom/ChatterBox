using ChatterBox.Data;
using ChatterBox.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatterBox.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == userId) ||
                           (m.SenderId == userId && m.ReceiverId == currentUser.Id))
                .OrderBy(m => m.SentAt)
                .Take(50)
                .Select(m => new
                {
                    m.MessageId,
                    m.Content,
                    m.SentAt,
                    m.SenderId,
                    SenderName = m.Sender.UserName,
                    m.ReceiverId,
                    ReceiverName = m.Receiver.UserName,
                    m.IsRead
                })
                .ToListAsync();

            return Json(messages);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null && message.ReceiverId == _userManager.GetUserId(User))
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
    }
}