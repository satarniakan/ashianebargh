using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application.Validators;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Services;

/// <summary>صدور و اعتبارسنجی توکن‌های API (اپ موبایل و کلاینت‌های غیرمرورگری).</summary>
public interface IApiTokenService
{
    /// <summary>تأیید OTP و صدور توکن در یک قدم — مسیر ورود اپ موبایل.</summary>
    Task<TokenIssueResult> VerifyOtpAndIssueTokenAsync(string phoneNumber, string code, string? deviceName);

    /// <summary>
    /// ورود با نام‌کاربری/ایمیل و رمز + صدور توکن — مسیر جایگزین وقتی سرویس پیامک قطع است.
    /// قفل موقت حساب دقیقاً مثل ورود وب اعمال می‌شود، ولی کوکی ست نمی‌شود.
    /// </summary>
    Task<TokenIssueResult> LoginWithPasswordAndIssueTokenAsync(
        string userName, string password, string? deviceName);

    /// <summary>صدور توکن برای کاربر موجود (مثلاً پس از ورود با رمز در پنل).</summary>
    Task<(string Token, DateTime ExpiresAtUtc)> IssueAsync(string userId, string? deviceName);

    /// <summary>اعتبارسنجی توکن خام و ساخت ClaimsPrincipal (با همان claimهای کوکی).</summary>
    Task<ClaimsPrincipal?> ValidateAsync(string token);

    Task<List<UserApiToken>> GetActiveAsync(string userId);

    /// <summary>لغو یک توکنِ خودِ کاربر. بازگشت: آیا توکنی لغو شد؟</summary>
    Task<bool> RevokeAsync(int tokenId, string userId);

    /// <summary>لغو همهٔ توکن‌ها («خروج از همهٔ دستگاه‌ها»). بازگشت: تعداد لغوشده.</summary>
    Task<int> RevokeAllAsync(string userId);
}

/// <param name="Succeeded">آیا ورود موفق بود؟</param>
/// <param name="Token">توکن خام — فقط همین‌بار دیده می‌شود.</param>
/// <param name="UserId">شناسهٔ کاربر برای ثبت تاریخچه (null یعنی کاربر یافت نشد).</param>
public record TokenIssueResult(bool Succeeded, string? Token = null, DateTime? ExpiresAtUtc = null,
    string? UserId = null);

public class ApiTokenService : IApiTokenService
{
    /// <summary>پیشوند توکن خام برای تشخیص در لاگ‌ها و هدرها (مثل GitHub's ghp_).</summary>
    public const string TokenPrefix = "pat_";

    private readonly IApiTokenRepository _tokens;
    private readonly IOtpService _otpService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _principalFactory;
    private readonly IAshianebarghUnitOfWork _unitOfWork;
    private readonly ISettingService _settings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiTokenService> _logger;
    private readonly TimeProvider _clock;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IAuditService? _audit;

    /// <summary>
    /// هشِ یک رمز تصادفیِ دورریختنی برای مسیر «کاربر ناموجود» — همان هزینهٔ PBKDF2
    /// مسیرِ «رمز اشتباه» تا اختلاف زمان، ثبت‌شده‌بودن نام کاربری را لو ندهد.
    /// </summary>
    private static readonly string DummyPasswordHash =
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N"));

    public ApiTokenService(
        IApiTokenRepository tokens,
        IOtpService otpService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserClaimsPrincipalFactory<ApplicationUser> principalFactory,
        IAshianebarghUnitOfWork unitOfWork,
        ISettingService settings,
        IConfiguration configuration,
        ILogger<ApiTokenService> logger,
        TimeProvider? clock = null,
        IPasswordHasher<ApplicationUser>? passwordHasher = null,
        IAuditService? audit = null)
    {
        _tokens = tokens;
        _otpService = otpService;
        _userManager = userManager;
        _signInManager = signInManager;
        _principalFactory = principalFactory;
        _unitOfWork = unitOfWork;
        _settings = settings;
        _configuration = configuration;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _passwordHasher = passwordHasher ?? new PasswordHasher<ApplicationUser>();
        _audit = audit;
    }

    public async Task<TokenIssueResult> VerifyOtpAndIssueTokenAsync(
        string phoneNumber, string code, string? deviceName)
    {
        // کد اول تأیید/مصرف می‌شود و بعد کاربر جست‌وجو می‌شود. ترتیب قبلی («اول کاربر»)
        // برای شمارهٔ ناموجود بلافاصله برمی‌گشت ولی برای شمارهٔ ثبت‌شده هش + کوئری + شمارندهٔ
        // تلاش می‌زد؛ همین اختلاف زمان به مهاجم می‌گفت کدام شماره ثبت شده است. حالا هر دو
        // مسیر یک کار انجام می‌دهند و شمارندهٔ ضدحدس هم برای شمارهٔ ناموجود اعمال می‌شود.
        // نرمال‌سازی مثل مسیر وب است (ارقام فارسی/عربی) تا هش و جست‌وجوی کاربر بخواند.
        phoneNumber = PersianSearch.NormalizePhone(phoneNumber);

        if (!await _otpService.VerifyOtpAsync(phoneNumber, code, OtpPurpose.ApiToken))
            return new TokenIssueResult(false);

        var user = await _userManager.FindByNameAsync(phoneNumber);
        if (user is null)
            return new TokenIssueResult(false);

        var issued = await IssueAsync(user.Id, deviceName);
        return new TokenIssueResult(true, issued.Token, issued.ExpiresAtUtc, user.Id);
    }

    public async Task<TokenIssueResult> LoginWithPasswordAndIssueTokenAsync(
        string userName, string password, string? deviceName)
    {
        // ورودی بلند پیش از کوئری/هش رد می‌شود (سقف ستون‌های Identity و هزینهٔ PBKDF2).
        if (!AuthInput.IsWithinLengthBounds(userName, password))
        {
            _logger.LogWarning("API password login failed: input exceeds length bounds");
            return new TokenIssueResult(false);
        }

        var user = await FindByUserNameOrEmailAsync(userName);
        if (user is null)
        {
            // همان هزینهٔ محاسباتیِ مسیرِ کاربرِ موجود تا زمان پاسخ، وجود حساب را لو ندهد.
            _passwordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash, password);
            _logger.LogWarning("API password login failed: no user found");
            return new TokenIssueResult(false);
        }

        // فقط بررسی رمز + قفل موقت — برخلاف PasswordSignInAsync، کوکی ست نمی‌شود
        var check = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            _logger.LogWarning("API password login failed: {Reason}",
                check.IsLockedOut ? "locked out" : "invalid password");
            return new TokenIssueResult(false, UserId: user.Id);
        }

        var issued = await IssueAsync(user.Id, deviceName);
        return new TokenIssueResult(true, issued.Token, issued.ExpiresAtUtc, user.Id);
    }

    public async Task<(string Token, DateTime ExpiresAtUtc)> IssueAsync(string userId, string? deviceName)
    {
        var raw = TokenPrefix + ToUrlSafe(RandomNumberGenerator.GetBytes(32));
        var now = _clock.GetUtcNow().UtcDateTime;
        var lifetimeText = await _settings.GetEffectiveAsync(
            IntegrationSettingKeys.ApiTokenLifetimeDays, "ApiTokens:LifetimeDays", "180");
        // سقف ۱۰ سال: مقدار تنظیماتِ نامعقول (مثلاً ۹۹۹۹۹۹) AddDays را سرریز می‌کرد
        // و ورود را با استثنا می‌شکست.
        var days = int.TryParse(lifetimeText, out var configured) && configured > 0 ? configured : 180;
        var expiresAt = now.AddDays(Math.Clamp(days, 1, 3650));

        // ستون device name محدود است؛ ورودی بلند در SaveChanges خطا می‌داد
        var normalizedDevice = deviceName?.Trim();
        if (normalizedDevice?.Length > 100)
            normalizedDevice = normalizedDevice[..100];

        await _tokens.AddAsync(new UserApiToken
        {
            UserId = userId,
            TokenHash = await HashTokenAsync(raw),
            DeviceName = string.IsNullOrEmpty(normalizedDevice) ? null : normalizedDevice,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAt
        });
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("API token issued for user {UserId}", userId);
        if (_audit is not null)
            await _audit.LogEventAsync("token.issued", null,
                $"توکن API برای دستگاه «{normalizedDevice ?? "نامشخص"}» صادر شد.", actorId: userId);
        return (raw, expiresAt);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var trimmed = token.Trim();
        var pepperedHash = await HashTokenAsync(trimmed);
        var record = await _tokens.GetByHashAsync(pepperedHash);

        // مسیر گذارِ «هش بدون pepper» فقط با کلید صریح Security:AllowLegacyUnpepperedHashes
        // باز می‌شود؛ پیش‌فرض بسته. توکن ۲۵۶ بیتی است پس ریسک حدس ندارد، ولی پذیرفتن
        // همیشگیِ هش بدون pepper یعنی یک نشت دیتابیس بدون نیاز به pepper کافی است.
        // بعد از چرخش pepper باید خاموش شود.
        if (record is null && _configuration.GetValue<bool>("Security:AllowLegacyUnpepperedHashes"))
        {
            var legacyHash = HashToken(trimmed, pepper: null);
            if (!string.Equals(pepperedHash, legacyHash, StringComparison.Ordinal))
                record = await _tokens.GetByHashAsync(legacyHash);
        }

        if (record is null || record.IsRevoked)
            return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        if (record.ExpiresAtUtc <= now)
            return null;

        var user = await _userManager.FindByIdAsync(record.UserId);
        if (user is null || await _userManager.IsLockedOutAsync(user))
            return null;

        // LastUsedAt حداکثر ساعتی یک‌بار به‌روز می‌شود تا هر درخواست API یک write اضافه نسازد
        if (record.LastUsedAtUtc is null || now - record.LastUsedAtUtc.Value >= TimeSpan.FromHours(1))
        {
            record.LastUsedAtUtc = now;
            await _unitOfWork.CompleteAsync();
        }

        // همان claimهای کوکی (نقش‌ها + مجوزها) — یک منبع حقیقت برای هر دو مسیر ورود
        return await _principalFactory.CreateAsync(user);
    }

    public async Task<List<UserApiToken>> GetActiveAsync(string userId)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var tokens = await _tokens.GetActiveForUserAsync(userId);
        return tokens.Where(t => t.ExpiresAtUtc > now).ToList();
    }

    public async Task<bool> RevokeAsync(int tokenId, string userId)
    {
        var tokens = await _tokens.GetActiveForUserAsync(userId);
        var token = tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token is null)
            return false;

        token.IsRevoked = true;
        token.RevokedAtUtc = _clock.GetUtcNow().UtcDateTime;
        await _unitOfWork.CompleteAsync();
        if (_audit is not null)
            await _audit.LogEventAsync("token.revoked", null, $"توکن API #{tokenId} لغو شد.", actorId: userId);
        return true;
    }

    public async Task<int> RevokeAllAsync(string userId)
    {
        var tokens = await _tokens.GetActiveForUserAsync(userId);
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAtUtc = now;
        }
        await _unitOfWork.CompleteAsync();
        if (_audit is not null && tokens.Count > 0)
            await _audit.LogEventAsync("token.revoked-all", null,
                $"{tokens.Count} توکن API لغو شد (خروج از همهٔ دستگاه‌ها).", actorId: userId);
        return tokens.Count;
    }

    /// <summary>
    /// هش توکن خام برای ذخیره‌سازی. عمومی تا تست بتواند ذخیرهٔ «فقط-هش» را اثبات کند.
    /// pepper سمت سرور (Security:Pepper) هم لحاظ می‌شود؛ خالی یعنی رفتار قدیمی (سازگاری).
    /// </summary>
    public static string HashToken(string raw, string? pepper = null)
    {
        var input = string.IsNullOrEmpty(pepper) ? raw : $"{raw}:{pepper}";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));
    }

    private async Task<string> HashTokenAsync(string raw)
    {
        var pepper = await _settings.GetEffectiveAsync(
            IntegrationSettingKeys.SecurityPepper, "Security:Pepper");
        return HashToken(raw, string.IsNullOrWhiteSpace(pepper) ? null : pepper);
    }

    private static string ToUrlSafe(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<ApplicationUser?> FindByUserNameOrEmailAsync(string userName)
    {
        var byEmail = await _userManager.FindByEmailAsync(userName);
        return byEmail ?? await _userManager.FindByNameAsync(userName);
    }
}
