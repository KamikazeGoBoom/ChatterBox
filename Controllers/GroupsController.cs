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

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var groups = await _context.Groups
                    .Include(g => g.CreatedBy)
                    .Include(g => g.Members)
                    .Where(g => !g.IsPrivate || g.Members.Any(m => m.UserId == currentUser.Id))
                    .ToListAsync();

                return View(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups");
                return View(Array.Empty<Group>());
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

            if (group.IsPrivate && !isMember)
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
            ViewBag.IsAdmin = group.CreatedById == currentUser.Id;
            ViewBag.Messages = messages;

            return View(group);
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

            var group = await _context.Groups.FindAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || group.CreatedById != currentUser.Id)
            {
                return Forbid();
            }

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

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || existingGroup.CreatedById != currentUser.Id)
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
                .Include(g => g.Members)
                .FirstOrDefaultAsync(m => m.GroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || group.CreatedById != currentUser.Id)
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
                if (currentUser == null || group.CreatedById != currentUser.Id)
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

                _logger.LogInformation($"Group {id} successfully deleted by user {currentUser.Id}");
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

                var membership = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == id && gm.UserId == currentUser.Id);

                if (membership != null)
                {
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

        private bool GroupExists(int id)
        {
            return _context.Groups.Any(e => e.GroupId == id);
        }
    }
}