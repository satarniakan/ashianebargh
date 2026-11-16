using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ashianebargh.Domain.Identity;

namespace Ashianebargh.Infrastructure;

/// <summary>
/// ساخت نقش‌های پایه و همگام‌کردن مجوزهای نقش ادمین با <see cref="IPermissionCatalog"/>.
/// نکتهٔ مهم: مجوزهایی که از کاتالوگ حذف شده‌اند هم از ادمین پاک می‌شوند،
/// وگرنه دسترسی قدیمی باقی می‌ماند و کسی نمی‌تواند آن را ببیند.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IReadOnlyList<string>? extraRoles = null)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var catalog = scope.ServiceProvider.GetRequiredService<IPermissionCatalog>();
        var knownPermissions = catalog.All.Select(p => p.Key).ToHashSet();

        foreach (var roleName in Roles.Default.Concat(extraRoles ?? []).Distinct())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(
                        $"ساخت نقش «{roleName}» ناموفق بود: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        var adminRole = await roleManager.FindByNameAsync(Roles.Admin);
        if (adminRole is null) return;

        var existingClaims = await roleManager.GetClaimsAsync(adminRole);
        var existingPermissions = existingClaims.Where(c => c.Type == Permissions.ClaimType).ToList();

        foreach (var permission in knownPermissions)
        {
            if (existingPermissions.Any(c => c.Value == permission)) continue;

            var addResult = await roleManager.AddClaimAsync(
                adminRole, new System.Security.Claims.Claim(Permissions.ClaimType, permission));
            if (!addResult.Succeeded)
                throw new InvalidOperationException(
                    $"افزودن مجوز «{permission}» به نقش ادمین ناموفق بود: {string.Join("; ", addResult.Errors.Select(e => e.Description))}");
        }

        var removedStalePermission = false;
        foreach (var staleClaim in existingPermissions.Where(c => !knownPermissions.Contains(c.Value)))
        {
            var removeResult = await roleManager.RemoveClaimAsync(adminRole, staleClaim);
            if (!removeResult.Succeeded)
                throw new InvalidOperationException(
                    $"حذف مجوز قدیمی «{staleClaim.Value}» از نقش ادمین ناموفق بود: {string.Join("; ", removeResult.Errors.Select(e => e.Description))}");
            removedStalePermission = true;
        }

        // مجوزها در کوکیِ نشست‌های زنده پخته شده‌اند؛ اگر کلیدی از کاتالوگ حذف شد،
        // ادمینِ واردشده همچنان آن را دارد. فقط در همان حالتِ نادر stamp عوض می‌شود
        // تا هر استقرارِ عادی همه را بیرون نیندازد.
        if (removedStalePermission)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var admin in await userManager.GetUsersInRoleAsync(Roles.Admin))
                await userManager.UpdateSecurityStampAsync(admin);
        }
    }
}