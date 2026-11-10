using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Infrastructure.Data;

namespace Ashianebargh.Infrastructure.Services;

public class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly IPermissionCatalog _catalog;
    private readonly AshianebarghDbContext _db;

    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        IPermissionCatalog catalog,
        AshianebarghDbContext db)
        : base(userManager, roleManager, options)
    {
        _catalog = catalog;
        _db = db;
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        // فقط FullName در پایه هست؛ اگر خالی باشد هیچ claimی اضافه نمی‌شود
        // و رابط کاربری می‌تواند از نام کاربری استفاده کند.
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            identity.AddClaim(new Claim("FullName", user.FullName));
        }

        // مجوزهای مؤثر کاربر در کوکی می‌نشینند تا policyها ([Authorize(Policy=...)])
        // و منو (NavMenu) بدون کوئری دیتابیس در هر درخواست کار کنند:
        // claimهای مستقیم کاربر + claimهای همهٔ نقش‌هایش. ادمین همهٔ کلیدهای
        // کاتالوگ را می‌گیرد تا با افزودن مجوز جدید، نیازی به لاگین مجدد نباشد.
        // توجه: تغییر مجوز نقش‌ها تا لاگین بعدی (یا بازاعتبارسنجی security stamp،
        // هر ۳۰ دقیقه) در کوکی‌های موجود اثر نمی‌کند.
        //
        // خواندن با سه کوئری ثابت انجام می‌شود (نه دو کوئری به‌ازای هر نقش)؛
        // قبلاً هر ورود/اعتبارسنجی توکن، چند رفت‌وبرگشت اضافه به دیتابیس می‌زد.
        var permissions = new HashSet<string>(Permissions.Always, StringComparer.Ordinal);

        var userClaims = await _db.UserClaims
            .Where(c => c.UserId == user.Id && c.ClaimType == Permissions.ClaimType)
            .Select(c => c.ClaimValue)
            .ToListAsync();
        foreach (var value in userClaims)
            if (value is not null) permissions.Add(value);

        var roleClaims = await _db.RoleClaims
            .Where(rc => rc.ClaimType == Permissions.ClaimType &&
                         _db.UserRoles.Any(ur => ur.RoleId == rc.RoleId && ur.UserId == user.Id))
            .Select(rc => rc.ClaimValue)
            .ToListAsync();
        foreach (var value in roleClaims)
            if (value is not null) permissions.Add(value);

        var isAdmin = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .AnyAsync(name => name == Roles.Admin);
        if (isAdmin)
        {
            foreach (var descriptor in _catalog.All)
                permissions.Add(descriptor.Key);
        }

        foreach (var permission in permissions)
            identity.AddClaim(new Claim(Permissions.ClaimType, permission));

        return principal;
    }
}