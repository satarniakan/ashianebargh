using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Application.Services;

/// <summary>
/// خواندن/نوشتن مجوزهای یک نقش. فهرست مجوزها از <see cref="IPermissionCatalog"/>
/// می‌آید که پروژهٔ مصرف‌کننده پیاده‌سازی‌اش می‌کند — پایه هیچ لیست ثابتی ندارد.
/// </summary>
public interface IPermissionService
{
    /// <summary>همهٔ مجوزها به‌همراه وضعیت اینکه آیا به نقش داده شده است.</summary>
    Task<IReadOnlyList<PermissionStateDto>> GetPermissionStatesAsync(string roleName);

    /// <summary>مجوزهای یک نقش، به‌همراه متن فارسی‌شان (برای صفحهٔ مدیریت مجوزها).</summary>
    Task<List<PermissionDescriptor>> GetPermissionsForRoleAsync(string roleName);

    Task<IdentityResult> SetPermissionsForRoleAsync(string roleName, IReadOnlyList<string> permissions);
}

/// <summary>وضعیت یک مجوز برای یک نقش.</summary>
/// <param name="Permission">توصیف کامل مجوز.</param>
/// <param name="IsGranted">آیا این نقش این مجوز را دارد؟</param>
public record PermissionStateDto(PermissionDescriptor Permission, bool IsGranted);

public class PermissionService : IPermissionService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IPermissionCatalog _catalog;
    private readonly IAshianebarghUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;

    public PermissionService(RoleManager<IdentityRole> roleManager, IPermissionCatalog catalog,
        IAshianebarghUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager,
        IAuditService audit, IHttpContextAccessor httpContext)
    {
        _roleManager = roleManager;
        _catalog = catalog;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _audit = audit;
        _httpContext = httpContext;
    }

    /// <summary>عامل فعلی (ادمین انجام‌دهنده) برای رد حسابرسی؛ بیرون از درخواست HTTP تهی است.</summary>
    private string? ActorName => _httpContext.HttpContext?.User.Identity?.Name;

    public async Task<IReadOnlyList<PermissionStateDto>> GetPermissionStatesAsync(string roleName)
    {
        var granted = await GetGrantedKeysAsync(roleName);
        return _catalog.All
            .Select(p => new PermissionStateDto(p, granted.Contains(p.Key)))
            .ToList();
    }

    public async Task<List<PermissionDescriptor>> GetPermissionsForRoleAsync(string roleName)
    {
        var granted = await GetGrantedKeysAsync(roleName);
        return _catalog.All.Where(p => granted.Contains(p.Key)).ToList();
    }

    public async Task<IdentityResult> SetPermissionsForRoleAsync(string roleName, IReadOnlyList<string> permissions)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
            return IdentityResult.Failed(new IdentityError { Description = "نقش یافت نشد." });

        // مجوزهای ناشناخته (تایپو یا مقدار ساختگی) عملاً هیچ دسترسی‌ای نمی‌دهند چون authorization
        // فقط مقادیر شناخته‌شده را می‌پذیرد — ولی بی‌صدا ذخیره می‌شدند و UI «ذخیره شد» نشان می‌داد.
        var knownKeys = _catalog.All.Select(p => p.Key).ToHashSet();
        var unknown = permissions.Except(knownKeys).ToList();
        if (unknown.Count > 0)
            return IdentityResult.Failed(new IdentityError
            {
                Description = $"این مجوزها ناشناخته‌اند: {string.Join("، ", unknown)}"
            });

        // حذف و افزودن باید اتمیک باشد: قبلاً اگر وسط کار خطایی می‌داد، نقش با
        // مجموعهٔ ناقص مجوزها می‌ماند و کاربرانش دسترسی از دست می‌دادند.
        IdentityResult? failure = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var currentClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in currentClaims.Where(c => c.Type == Permissions.ClaimType))
                {
                    var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
                    if (!removeResult.Succeeded)
                    {
                        failure = removeResult;
                        throw new PermissionUpdateAbortedException();
                    }
                }

                foreach (var permission in permissions.Distinct())
                {
                    var addResult = await _roleManager.AddClaimAsync(
                        role, new Claim(Permissions.ClaimType, permission));
                    if (!addResult.Succeeded)
                    {
                        failure = addResult;
                        throw new PermissionUpdateAbortedException();
                    }
                }

                // مجوزهای نقش هنگام ورود در کوکی احراز هویت پخته می‌شوند؛ بدون باطل‌کردن
                // stampِ کاربرانِ همین نقش، نشست‌های زنده تا لاگین بعدی مجوزهای قبلی را
                // نگه می‌دارند (گرفتن مجوزِ تازه هم دیده نمی‌شد). این کار داخل همان تراکنش
                // است تا «مجوز عوض شد ولی نشستِ کسی باطل نشد» پیش نیاید.
                // تغییر مجوز نقش یک عملیات نادر ادمینی است، پس پیمایش کاربران نقش
                // (نه همهٔ کاربران) هزینهٔ قابل قبولی دارد.
                foreach (var member in await _userManager.GetUsersInRoleAsync(roleName))
                {
                    var stampResult = await _userManager.UpdateSecurityStampAsync(member);
                    if (!stampResult.Succeeded)
                    {
                        failure = stampResult;
                        throw new PermissionUpdateAbortedException();
                    }
                }
            });
        }
        catch (PermissionUpdateAbortedException)
        {
            _unitOfWork.ClearChangeTracker();
            return failure!;
        }

        // تغییر مجوز یک رویداد امنیتی است و قبلاً هیچ رد حسابرسی نداشت.
        await _audit.LogEventAsync("role.permissions-changed", ActorName,
            $"مجوزهای نقش «{roleName}» به «{string.Join("، ", permissions.Distinct())}» تغییر کرد.");

        return IdentityResult.Success;
    }

    /// <summary>فقط برای شکستن تراکنش پس از ثبت نتیجهٔ Identity؛ بیرون نمی‌رود.</summary>
    private sealed class PermissionUpdateAbortedException : Exception
    {
    }

    private async Task<HashSet<string>> GetGrantedKeysAsync(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null) return [];

        var claims = await _roleManager.GetClaimsAsync(role);
        return claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();
    }
}