using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ChatterBox.Models;

namespace ChatterBox.Services
{
    public class AdminInitializationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminInitializationService> _logger;

        public AdminInitializationService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminInitializationService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting admin initialization...");

                // Create SystemAdmin role if it doesn't exist
                if (!await _roleManager.RoleExistsAsync("SystemAdmin"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("SystemAdmin"));
                    _logger.LogInformation("Created SystemAdmin role");
                }

                // Create or update admin user
                var adminEmail = "admin@chatterbox.com";
                var adminUserName = "admin@chatterbox.com"; // Keep username same as email

                // Try to find user by email first
                var adminUser = await _userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    // Create new admin user
                    adminUser = new ApplicationUser
                    {
                        UserName = adminUserName,
                        NormalizedUserName = adminUserName.ToUpper(),
                        Email = adminEmail,
                        NormalizedEmail = adminEmail.ToUpper(),
                        EmailConfirmed = true,
                        Status = "Online",
                        Birthday = DateTime.UtcNow,
                        LastSeen = DateTime.UtcNow,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    _logger.LogInformation("Creating new admin user...");
                    var result = await _userManager.CreateAsync(adminUser, "Admin123!");

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Admin user created successfully");

                        // Add to admin role
                        await _userManager.AddToRoleAsync(adminUser, "SystemAdmin");
                        _logger.LogInformation("Added admin to SystemAdmin role");
                    }
                    else
                    {
                        _logger.LogError("Failed to create admin user: {Errors}",
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogInformation("Admin user exists, updating...");

                    // Update user properties
                    adminUser.UserName = adminUserName;
                    adminUser.NormalizedUserName = adminUserName.ToUpper();
                    adminUser.Email = adminEmail;
                    adminUser.NormalizedEmail = adminEmail.ToUpper();
                    adminUser.EmailConfirmed = true;

                    await _userManager.UpdateAsync(adminUser);
                    _logger.LogInformation("Updated admin user properties");

                    // Ensure in admin role
                    if (!await _userManager.IsInRoleAsync(adminUser, "SystemAdmin"))
                    {
                        await _userManager.AddToRoleAsync(adminUser, "SystemAdmin");
                        _logger.LogInformation("Added existing user to SystemAdmin role");
                    }

                    // Reset password
                    var token = await _userManager.GeneratePasswordResetTokenAsync(adminUser);
                    var resetResult = await _userManager.ResetPasswordAsync(adminUser, token, "Admin123!");

                    if (resetResult.Succeeded)
                    {
                        _logger.LogInformation("Reset admin password successfully");
                    }
                    else
                    {
                        _logger.LogError("Failed to reset admin password: {Errors}",
                            string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                    }
                }

                // Verify final setup
                var verifyUser = await _userManager.FindByEmailAsync(adminEmail);
                if (verifyUser != null)
                {
                    var inRole = await _userManager.IsInRoleAsync(verifyUser, "SystemAdmin");
                    _logger.LogInformation("Final verification - User exists: true, In admin role: {InRole}", inRole);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AdminInitializationService");
                throw;
            }
        }
    }
}