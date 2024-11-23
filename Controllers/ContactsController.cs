using ChatterBox.Data;
using ChatterBox.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatterBox.Controllers
{
    [Authorize]
    public class ContactsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ContactsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var contacts = await _context.Contacts
                .Include(c => c.ContactUser)
                .Where(c => c.UserId == currentUser.Id && !c.IsBlocked)
                .Select(c => c.ContactUser)
                .ToListAsync();

            return View(contacts);
        }

        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var users = await _userManager.Users
                .Where(u => u.Id != currentUser.Id &&
                           (u.UserName.Contains(searchTerm) || u.Email.Contains(searchTerm)))
                .Take(10)
                .ToListAsync();

            return Json(users.Select(u => new { u.Id, u.UserName, u.Email }));
        }

        [HttpPost]
        public async Task<IActionResult> Add(string contactId)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var contact = new Contact
            {
                UserId = currentUser.Id,
                ContactUserId = contactId,
                CreatedAt = DateTime.UtcNow,
                IsBlocked = false
            };

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Remove(string contactId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.UserId == currentUser.Id && c.ContactUserId == contactId);

            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }
    }
}