using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChatterBox.Data;
using ChatterBox.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ChatterBox.Controllers
{
    [Authorize]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<GroupsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        private async Task<bool> IsSystemAdmin()
        {
            var user = await _userManager.GetUserAsync(User);
            return user != null && await _userManager.IsInRoleAsync(user, "SystemAdmin");
        }

        private async Task<bool> IsGroupAdmin(int groupId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return false;

            var group = await _context.Groups.FindAsync(groupId);
            return group?.CurrentAdminId == currentUser.Id || await IsSystemAdmin();
        }

        private async Task<GroupMember> GetOldestMember(int groupId, string excludeUserId)
        {
            return await _context.GroupMembers
                .Where(m => m.GroupId == groupId && m.UserId != excludeUserId)
                .OrderBy(m => m.JoinedAt)
                .FirstOrDefaultAsync();
        }

        private async Task TransferAdminRole(int groupId, string newAdminId)
        {
            var group = await _context.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.GroupId == groupId);

            if (group != null)
            {
                // Update current admin's role and timestamps
                var currentAdmin = group.Members.FirstOrDefault(m => m.UserId == group.CurrentAdminId);
                if (currentAdmin != null)
                {
                    currentAdmin.Role = "Member";
                    currentAdmin.WasAdmin = true;
                    currentAdmin.StoppedBeingAdminAt = DateTime.UtcNow;
                }

                // Update new admin's role and timestamps
                var newAdmin = group.Members.FirstOrDefault(m => m.UserId == newAdminId);
                if (newAdmin != null)
                {
                    newAdmin.Role = "Admin";
                    newAdmin.BecameAdminAt = DateTime.UtcNow;
                }

                // Update group's admin tracking
                group.CurrentAdminId = newAdminId;
                group.LastAdminTransferDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Admin role transferred in group {groupId} to user {newAdminId}");
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var isAdmin = await IsSystemAdmin();

                var groups = isAdmin
                    ? await _context.Groups
                        .Include(g => g.CreatedBy)
                        .Include(g => g.CurrentAdmin)
                        .Include(g => g.Members)
                        .ToListAsync()
                    : await _context.Groups
                        .Include(g => g.CreatedBy)
                        .Include(g => g.CurrentAdmin)
                        .Include(g => g.Members)
                        .Where(g => !g.IsPrivate || g.Members.Any(m => m.UserId == currentUser.Id))
                        .ToListAsync();

                // Initialize all required ViewBag properties
                ViewBag.IsSystemAdmin = isAdmin;                    // Make sure this is set
                ViewBag.CurrentUserId = currentUser.Id;            // Make sure this is set
                return View(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups");
                // Initialize ViewBag even in error case
                ViewBag.IsSystemAdmin = false;
                ViewBag.CurrentUserId = null;
                return View(Array.Empty<Group>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferAdmin(int groupId, string newAdminId)
        {
            try
            {
                var group = await _context.Groups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.GroupId == groupId);

                if (group == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                if (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin())
                {
                    return Forbid();
                }

                var newAdminMember = group.Members.FirstOrDefault(m => m.UserId == newAdminId);
                if (newAdminMember == null)
                {
                    return BadRequest("New admin must be a group member");
                }

                await TransferAdminRole(groupId, newAdminId);
                return RedirectToAction(nameof(Details), new { id = groupId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring admin role in group {groupId}");
                return RedirectToAction(nameof(Details), new { id = groupId });
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Groups
                .Include(g => g.CreatedBy)
                .Include(g => g.CurrentAdmin)
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(m => m.GroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var isMember = group.Members.Any(m => m.UserId == currentUser.Id);
            var isSystemAdmin = await IsSystemAdmin();
            var isAdmin = group.CurrentAdminId == currentUser.Id;

            if (group.IsPrivate && !isMember && !isSystemAdmin)
            {
                return Forbid();
            }

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.GroupId == id)
                .OrderByDescending(m => m.SentAt)
                .Take(50)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUser.Id;
            ViewBag.IsMember = isMember;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsSystemAdmin = isSystemAdmin;
            ViewBag.Messages = messages;

            return View(group);
        }

        public async Task<IActionResult> ManageMembers(int id)
        {
            var group = await _context.Groups
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.GroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin())
            {
                return Forbid();
            }

            var memberIds = group.Members.Select(m => m.UserId).ToList();
            var nonMembers = await _userManager.Users
                .Where(u => !memberIds.Contains(u.Id))
                .ToListAsync();

            ViewBag.NonMembers = nonMembers;
            ViewBag.IsSystemAdmin = await IsSystemAdmin();
            ViewBag.CurrentUserId = currentUser.Id;
            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(int groupId, string userId)
        {
            try
            {
                var group = await _context.Groups.FindAsync(groupId);
                var user = await _userManager.FindByIdAsync(userId);

                if (group == null || user == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                if (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin())
                {
                    return Forbid();
                }

                var membership = new GroupMember
                {
                    GroupId = groupId,
                    UserId = userId,
                    Role = "Member",
                    JoinedAt = DateTime.UtcNow
                };

                _context.GroupMembers.Add(membership);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} added to group {groupId} by {currentUser.Id}");
                return RedirectToAction(nameof(ManageMembers), new { id = groupId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding user to group {groupId}");
                return RedirectToAction(nameof(ManageMembers), new { id = groupId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int groupId, string userId)
        {
            try
            {
                var membership = await _context.GroupMembers
                    .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

                if (membership == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var group = await _context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return NotFound();
                }

                if (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin())
                {
                    return Forbid();
                }

                // Don't allow removal of the current admin
                if (userId == group.CurrentAdminId)
                {
                    return BadRequest("Cannot remove the current admin. Transfer admin role first.");
                }

                _context.GroupMembers.Remove(membership);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} removed from group {groupId} by {currentUser.Id}");
                return RedirectToAction(nameof(ManageMembers), new { id = groupId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing user from group {groupId}");
                return RedirectToAction(nameof(ManageMembers), new { id = groupId });
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,IsPrivate")] Group groupInput)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                if (string.IsNullOrEmpty(groupInput.Name))
                {
                    ModelState.AddModelError("Name", "Group name is required");
                    return View(groupInput);
                }

                var group = new Group
                {
                    Name = groupInput.Name,
                    IsPrivate = groupInput.IsPrivate,
                    CreatedById = currentUser.Id,
                    CreatedBy = currentUser,
                    CurrentAdminId = currentUser.Id,
                    CurrentAdmin = currentUser,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Groups.Add(group);
                await _context.SaveChangesAsync();

                var membership = new GroupMember
                {
                    GroupId = group.GroupId,
                    UserId = currentUser.Id,
                    Role = "Admin",
                    JoinedAt = DateTime.UtcNow,
                    BecameAdminAt = DateTime.UtcNow,
                    User = currentUser,
                    Group = group
                };

                _context.GroupMembers.Add(membership);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group");
                ModelState.AddModelError("", "Error creating group");
                return View(groupInput);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.GroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            // Updated permission check to use CurrentAdminId
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin()))
            {
                return Forbid();
            }

            // Get list of members for admin transfer dropdown
            var members = group.Members
                .Where(m => m.UserId != currentUser.Id)
                .Select(m => m.User)
                .ToList();

            ViewBag.PotentialAdmins = members;
            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("GroupId,Name,IsPrivate")] Group group)
        {
            if (id != group.GroupId)
            {
                return NotFound();
            }

            var existingGroup = await _context.Groups.FindAsync(id);
            if (existingGroup == null)
            {
                return NotFound();
            }

            // Updated permission check to use CurrentAdminId
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || (existingGroup.CurrentAdminId != currentUser.Id && !await IsSystemAdmin()))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingGroup.Name = group.Name;
                    existingGroup.IsPrivate = group.IsPrivate;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id = group.GroupId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GroupExists(group.GroupId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(group);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var group = await _context.Groups
                .Include(g => g.CreatedBy)
                .Include(g => g.CurrentAdmin)
                .Include(g => g.Members)
                .FirstOrDefaultAsync(m => m.GroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin()))
            {
                return Forbid();
            }

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var group = await _context.Groups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.GroupId == id);

                if (group == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null || (group.CurrentAdminId != currentUser.Id && !await IsSystemAdmin()))
                {
                    return Forbid();
                }

                // Delete all group messages first
                var messages = await _context.Messages
                    .Where(m => m.GroupId == id)
                    .ToListAsync();
                _context.Messages.RemoveRange(messages);
                await _context.SaveChangesAsync();

                // Delete all group members
                _context.GroupMembers.RemoveRange(group.Members);
                await _context.SaveChangesAsync();

                // Finally delete the group
                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Group {id} successfully deleted by {currentUser.Id}");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting group {id}");
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int id)
        {
            try
            {
                var group = await _context.Groups.FindAsync(id);
                if (group == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var existingMembership = await _context.GroupMembers
                    .AnyAsync(gm => gm.GroupId == id && gm.UserId == currentUser.Id);

                if (existingMembership)
                {
                    return RedirectToAction(nameof(Details), new { id });
                }

                var membership = new GroupMember
                {
                    GroupId = id,
                    UserId = currentUser.Id,
                    Role = "Member",
                    JoinedAt = DateTime.UtcNow,
                    User = currentUser,
                    Group = group
                };

                _context.GroupMembers.Add(membership);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining group {id}");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var group = await _context.Groups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.GroupId == id);

                if (group == null)
                {
                    return NotFound();
                }

                var membership = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == id && gm.UserId == currentUser.Id);

                if (membership != null)
                {
                    // If leaving user is admin, transfer admin role to oldest member
                    if (group.CurrentAdminId == currentUser.Id)
                    {
                        var oldestMember = await GetOldestMember(id, currentUser.Id);
                        if (oldestMember != null)
                        {
                            await TransferAdminRole(id, oldestMember.UserId);
                        }
                        else
                        {
                            // If no other members, delete the group
                            return await DeleteConfirmed(id);
                        }
                    }

                    _context.GroupMembers.Remove(membership);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error leaving group {id}");
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DirectMessage(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Check if the user is in contacts
            var isContact = await _context.Contacts
                .AnyAsync(c => c.UserId == currentUser.Id && c.ContactUserId == id);

            if (isContact)
            {
                return RedirectToAction("Index", "Chat", new { userId = id });
            }

            // If not a contact, redirect to contacts page
            return RedirectToAction("Index", "Contacts");
        }

        private bool GroupExists(int id)
        {
            return _context.Groups.Any(e => e.GroupId == id);
        }
    }
}