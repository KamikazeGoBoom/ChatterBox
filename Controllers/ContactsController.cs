using ChatterBox.Data;
using ChatterBox.Models;
using ChatterBox.Services;
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
        private readonly INotificationService _notificationService;

        public ContactsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ContactsController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Get accepted contacts
            var contacts = await _context.Contacts
                .Include(c => c.ContactUser)
                .Where(c => c.UserId == currentUser.Id && !c.IsBlocked && c.Status == ContactStatus.Accepted)
                .Select(c => c.ContactUser)
                .ToListAsync();

            // Get pending incoming requests
            var pendingRequests = await _context.Contacts
                .Include(c => c.User)
                .Where(c => c.ContactUserId == currentUser.Id && c.Status == ContactStatus.Pending)
                .Select(c => c.User)
                .ToListAsync();

            // Get pending outgoing requests
            var outgoingRequests = await _context.Contacts
                .Include(c => c.ContactUser)
                .Where(c => c.UserId == currentUser.Id && c.Status == ContactStatus.Pending)
                .Select(c => c.ContactUser)
                .ToListAsync();

            ViewData["PendingRequests"] = pendingRequests;
            ViewData["OutgoingRequests"] = outgoingRequests;

            return View(contacts);
        }

        public async Task<IActionResult> GetContacts()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var contacts = await _context.Contacts
                .Include(c => c.ContactUser)
                .Where(c => c.UserId == currentUser.Id && !c.IsBlocked && c.Status == ContactStatus.Accepted)
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
            return Json(new { id = user.Id, userName = user.UserName, status = user.Status });
        }

        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Json(new List<object>());

            var currentUser = await _userManager.GetUserAsync(User);

            // Get all existing contacts and requests
            var existingContacts = await _context.Contacts
                .Where(c => c.UserId == currentUser.Id || c.ContactUserId == currentUser.Id)
                .Select(c => c.UserId == currentUser.Id ? c.ContactUserId : c.UserId)
                .ToListAsync();

            var users = await _userManager.Users
                .Where(u => u.Id != currentUser.Id &&
                           !existingContacts.Contains(u.Id) &&
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
                    .FirstOrDefaultAsync(c =>
                        (c.UserId == currentUser.Id && c.ContactUserId == contactId) ||
                        (c.UserId == contactId && c.ContactUserId == currentUser.Id));

                if (existingContact != null)
                    return Json(new { success = false, message = "Contact request already exists" });

                var contact = new Contact
                {
                    UserId = currentUser.Id,
                    ContactUserId = contactId,
                    CreatedAt = DateTime.UtcNow,
                    IsBlocked = false,
                    Status = ContactStatus.Pending
                };

                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();

                // Send notification to the contact user
                await _notificationService.CreateNotificationAsync(
                    contactId,
                    "New Contact Request",
                    $"{currentUser.UserName} wants to add you as a contact",
                    "ContactRequest",
                    currentUser.Id
                );

                return Json(new { success = true, message = "Contact request sent" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contact");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> WithdrawRequest(string contactId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var request = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id &&
                                            c.ContactUserId == contactId &&
                                            c.Status == ContactStatus.Pending);

                if (request == null)
                    return Json(new { success = false, message = "Request not found" });

                _context.Contacts.Remove(request);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing contact request");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRequest(string userId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var request = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.UserId == userId &&
                                            c.ContactUserId == currentUser.Id &&
                                            c.Status == ContactStatus.Pending);

                if (request == null)
                    return Json(new { success = false, message = "Request not found" });

                request.Status = ContactStatus.Accepted;

                // Create reciprocal contact
                var reciprocalContact = new Contact
                {
                    UserId = currentUser.Id,
                    ContactUserId = userId,
                    Status = ContactStatus.Accepted,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Contacts.Add(reciprocalContact);
                await _context.SaveChangesAsync();

                // Send notification to the user who sent the request
                await _notificationService.CreateNotificationAsync(
                    userId,
                    "Contact Request Accepted",
                    $"{currentUser.UserName} accepted your contact request",
                    "ContactAccepted",
                    currentUser.Id
                );

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting contact request");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeclineRequest(string userId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var request = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.UserId == userId &&
                                            c.ContactUserId == currentUser.Id &&
                                            c.Status == ContactStatus.Pending);

                if (request == null)
                    return Json(new { success = false, message = "Request not found" });

                _context.Contacts.Remove(request);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining contact request");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Remove(string contactId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Remove both directions of the contact relationship
                var contacts = await _context.Contacts
                    .Where(c => (c.UserId == currentUser.Id && c.ContactUserId == contactId) ||
                               (c.UserId == contactId && c.ContactUserId == currentUser.Id))
                    .ToListAsync();

                if (contacts.Any())
                {
                    _context.Contacts.RemoveRange(contacts);
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