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
        private readonly ILogger<ContactsController> _logger;

        public ContactsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ContactsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
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

        public async Task<IActionResult> GetContacts()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var contacts = await _context.Contacts
                .Include(c => c.ContactUser)
                .Where(c => c.UserId == currentUser.Id && !c.IsBlocked)
                .Select(c => new
                {
                    id = c.ContactUser.Id,
                    userName = c.ContactUser.UserName,
                    status = c.ContactUser.Status
                })
                .ToListAsync();

            return Json(contacts);
        }

        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();
            return Json(new { id = user.Id, userName = user.UserName, status = user.Status });
        }

        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Json(new List<object>());

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Json(new List<object>());

            var currentContacts = await _context.Contacts
                .Where(c => c.UserId == currentUser.Id)
                .Select(c => c.ContactUserId)
                .ToListAsync();

            var users = await _userManager.Users
                .Where(u => u.Id != currentUser.Id &&
                           !currentContacts.Contains(u.Id) &&
                           (u.UserName.Contains(searchTerm) || u.Email.Contains(searchTerm)))
                .Take(10)
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();

            return Json(users);
        }

        [HttpPost]
        public async Task<IActionResult> Add(string contactId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Json(new { success = false, message = "Current user not found" });

                var contactUser = await _userManager.FindByIdAsync(contactId);
                if (contactUser == null)
                    return Json(new { success = false, message = "Contact user not found" });

                var existingContact = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id && c.ContactUserId == contactId);

                if (existingContact != null)
                    return Json(new { success = false, message = "Contact already exists" });

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contact");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Remove(string contactId)
        {
            try
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

                return Json(new { success = false, message = "Contact not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing contact");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}