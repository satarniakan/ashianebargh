using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ashianebargh.Application.DTOs;
using Ashianebargh.Application.Helpers;
using Ashianebargh.Domain.Enums;
using Ashianebargh.Domain.Identity;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Domain.Queries;

namespace Ashianebargh.Application.Services;

public interface IAuthService
{
    Task<PasswordLoginResult> LoginWithPasswordAsync(string userName, string password);
    Task RequestOtpAsync(string phoneNumber);
    Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string code);
    Task LogoutAsync();
    Task<UserProfileDto?> GetProfileAsync(string userId);
    Task<ProfileUpdateResult> UpdateProfileAsync(
        string userId, string? fullName, string? email, string? password, string? confirmPassword);

    /// <summary>
    /// تعیین رمز جدید با کد پیامکی (فراموشی رمز موبایل): کد مصرف می‌شود و بعد رمز
    /// با توکن Identity ریست می‌شود. برای کاربرِ بی‌رمز هم کار می‌کند (تعیین اول).
    /// </summary>
    Task<PasswordResetResult> ResetPasswordWithOtpAsync(
        string phoneNumber, string code, string newPassword, string? confirmPassword);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOtpService _otpService;
    private readonly ILogger<AuthService> _logger;
    private readonly INotificationService _notifications;
    private readonly ILoginHistoryService _history;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IApiTokenService _tokens;
    private readonly IAshianebarghUnitOfWork _unitOfWork;
    private readonly string? _firstAdminPhoneNumber;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOtpService otpService,
        INotificationService notifications,
        ILoginHistoryService history,
        IHttpContextAccessor httpContext,
        IApiTokenService tokens,
        IAshianebarghUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _otpService = otpService;
        _notifications = notifications;
        _history = history;
        _httpContext = httpContext;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _logger = logger;
        // شمارهٔ ادمین اول فقط از تنظیمات «Identity:FirstAdminPhoneNumber» خوانده می‌شود.
        // اگر تنظیم نباشد، هیچ شماره‌ای خودکار نقش Admin نمی‌گیرد.
        // هر دو سمت مقایسه نرمال می‌شوند (ارقام فارسیِ تنظیمات هم ممکن است فارسی تایپ شده باشد).
        _firstAdminPhoneNumber = PersianSearch.NormalizePhone(configuration["Identity:FirstAdminPhoneNumber"]);
    }

    private (string? Ip, string? UserAgent) RequestInfo()
    {
        var http = _httpContext.HttpContext;
        return (http?.Connection.RemoteIpAddress?.ToString(),
            http?.Request.Headers.UserAgent.ToString());
    }

    public async Task<PasswordLoginResult> LoginWithPasswordAsync(string userName, string password)
    {
        var (ip, userAgent) = RequestInfo();
        var user = await FindByUserNameOrEmailAsync(userName);

        if (user is null)
        {
            _logger.LogWarning("Login failed: no user found for {UserName}", userName);
            await _history.RecordAsync(null, userName, false, "Password", ip, userAgent, "invalid-credentials");
            return new PasswordLoginResult(false);
        }

        // lockoutOnFailure:true (پیش‌فرض Identity خودش FALSE است) قفل موقت حساب را می‌خواهد.
        var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            var reason = result.IsLockedOut ? "locked-out" : "invalid-credentials";
            _logger.LogWarning("Login failed for {UserName}: {Reason}", userName,
                result.IsLockedOut ? "locked out" : result.IsNotAllowed ? "not allowed" : "invalid password");
            await _history.RecordAsync(user.Id, userName, false, "Password", ip, userAgent, reason);
        }
        else
        {
            await _history.RecordAsync(user.Id, userName, true, "Password", ip, userAgent);
        }

        return new PasswordLoginResult(result.Succeeded);
    }

    public Task RequestOtpAsync(string phoneNumber) =>
        _otpService.GenerateAndSendOtpAsync(PersianSearch.NormalizePhone(phoneNumber));

    public async Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string code)
    {
        // ارقام فارسی/عربیِ شماره به انگلیسی — وگرنه هش OTP و جست‌وجوی کاربر با
        // چیزی که هنگام درخواست بود نمی‌خواند. نرمال‌سازی idempotent است.
        phoneNumber = PersianSearch.NormalizePhone(phoneNumber);

        var (ip, userAgent) = RequestInfo();

        if (!await _otpService.VerifyOtpAsync(phoneNumber, code))
        {
            await _history.RecordAsync(null, phoneNumber, false, "Otp", ip, userAgent, "invalid-otp");
            return new OtpVerificationResult(false, false);
        }

        var user = await _userManager.FindByNameAsync(phoneNumber);
        var isNewUser = user is null;
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = phoneNumber,
                PhoneNumber = phoneNumber,
                PhoneNumberConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                // دو درخواست موازی VerifyOtp برای شمارهٔ جدید: درخواست بازنده با
                // DuplicateUserName شکست می‌خورد — کاربر تازه‌ساخته‌شده را دوباره می‌خوانیم
                // و به‌جای برگرداندن «ناموفق»، او را لاگین می‌کنیم.
                if (createResult.Errors.Any(e => e.Code == "DuplicateUserName"))
                {
                    user = await _userManager.FindByNameAsync(phoneNumber);
                    isNewUser = false;
                }

                if (user is null)
                {
                    _logger.LogWarning("User creation failed for {PhoneNumber}: {Errors}",
                        PiiMask.Phone(phoneNumber), string.Join(" | ", createResult.Errors.Select(e => e.Description)));
                    await _history.RecordAsync(null, phoneNumber, false, "Otp", ip, userAgent, "user-creation-failed");
                    return new OtpVerificationResult(false, false);
                }
            }

            if (isNewUser)
            {
                // نقش پیش‌فرضِ کاربر تازه‌وارد «کاربر» است، نه ادمین. تنها استثنا شماره‌ای
                // است که در «Identity:FirstAdminPhoneNumber» تنظیم شده — همان «ادمین اول»
                // روی دیتابیس خالی. بدون این گارد، هر شماره‌ای که OTP را تأیید می‌کرد
                // خودکار ادمین می‌شد (نقص امنیتی). قانون در Roles.DefaultRoleFor است.
                await _userManager.AddToRoleAsync(
                    user, Roles.DefaultRoleFor(_firstAdminPhoneNumber, phoneNumber));

                await _notifications.NotifyRoleAsync(Roles.Admin, "کاربر جدید ثبت‌نام کرد",
                    $"شماره {phoneNumber}", NotificationType.System, "/admin/users");
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        await _history.RecordAsync(user.Id, phoneNumber, true, "Otp", ip, userAgent);
        return new OtpVerificationResult(true, isNewUser);
    }

    public Task LogoutAsync() => _signInManager.SignOutAsync();

    public async Task<UserProfileDto?> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        return new UserProfileDto(
            user.UserName,
            user.PhoneNumber,
            user.FullName,
            user.Email,
            await _userManager.HasPasswordAsync(user));
    }

    public async Task<ProfileUpdateResult> UpdateProfileAsync(
        string userId, string? fullName, string? email, string? password, string? confirmPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return new ProfileUpdateResult(ProfileUpdateStatus.UserNotFound);

        // همهٔ بررسی‌ها پیش از هر نوشتن انجام می‌شوند. قبلاً نام پیش از بررسی ایمیل/رمز
        // ذخیره می‌شد و خطای بعدی، پروفایل را نیمه‌کاره رها می‌کرد.
        string? trimmedName = null;
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            trimmedName = fullName.Trim();
            if (trimmedName.Length > 100)
                return new ProfileUpdateResult(ProfileUpdateStatus.FullNameTooLong);
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (normalizedEmail is not null)
        {
            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser is not null && existingUser.Id != user.Id)
                return new ProfileUpdateResult(ProfileUpdateStatus.EmailAlreadyExists);
        }

        var setPassword = !string.IsNullOrWhiteSpace(password);
        if (setPassword)
        {
            // نشست جانشین حق تعیین رمزِ کاربر را ندارد — وگرنه ادمین پشتیبانی می‌توانست
            // برای همیشه راه ورود رمزی به حساب کاربر باز کند
            if (_httpContext.HttpContext?.User.HasClaim(c => c.Type == ImpersonationClaims.Impersonator) == true)
                return new ProfileUpdateResult(ProfileUpdateStatus.ImpersonationBlocked);

            if (await _userManager.HasPasswordAsync(user))
                return new ProfileUpdateResult(ProfileUpdateStatus.PasswordAlreadySet);

            if (password != confirmPassword)
                return new ProfileUpdateResult(ProfileUpdateStatus.PasswordMismatch);
        }

        ProfileUpdateStatus? failure = null;
        try
        {
            // سه عملیات Identity جدا (نام/ایمیل/رمز) در یک تراکنش: شکست هر کدام،
            // بقیه را هم برمی‌گرداند تا کاربر با دادهٔ نیمه‌ذخیره روبه‌رو نشود.
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (trimmedName is not null)
                {
                    user.FullName = trimmedName;
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        _logger.LogWarning("Profile name update failed for user {UserId}: {Errors}",
                            userId, string.Join(" | ", updateResult.Errors.Select(e => e.Description)));
                        failure = ProfileUpdateStatus.ProfileUpdateFailed;
                        throw new ProfileUpdateAbortedException();
                    }
                }

                if (normalizedEmail is not null)
                {
                    var emailResult = await _userManager.SetEmailAsync(user, normalizedEmail);
                    if (!emailResult.Succeeded)
                    {
                        _logger.LogWarning("SetEmail failed for user {UserId}: {Errors}",
                            userId, string.Join(" | ", emailResult.Errors.Select(e => e.Description)));
                        failure = ProfileUpdateStatus.EmailUpdateFailed;
                        throw new ProfileUpdateAbortedException();
                    }
                }

                if (setPassword)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var passwordResult = await _userManager.ResetPasswordAsync(user, token, password!);
                    if (!passwordResult.Succeeded)
                    {
                        failure = ProfileUpdateStatus.PasswordUpdateFailed;
                        throw new ProfileUpdateAbortedException();
                    }
                }
            });
        }
        catch (ProfileUpdateAbortedException)
        {
            _unitOfWork.ClearChangeTracker();
            return new ProfileUpdateResult(failure!.Value);
        }

        return new ProfileUpdateResult(ProfileUpdateStatus.Success);
    }

    /// <summary>فقط برای شکستن تراکنشِ پروفایل پس از ثبت وضعیت خطا؛ بیرون نمی‌رود.</summary>
    private sealed class ProfileUpdateAbortedException : Exception
    {
    }

    private async Task<ApplicationUser?> FindByUserNameOrEmailAsync(string userName)
    {
        var byEmail = await _userManager.FindByEmailAsync(userName);
        return byEmail ?? await _userManager.FindByNameAsync(userName);
    }

    public async Task<PasswordResetResult> ResetPasswordWithOtpAsync(
        string phoneNumber, string code, string newPassword, string? confirmPassword)
    {
        phoneNumber = PersianSearch.NormalizePhone(phoneNumber);

        // عدم تطابق پیش از مصرف کد بررسی می‌شود: کد یک‌بارمصرف است و کاربر نباید
        // به‌خاطر تایپ اشتباه رمز (خطای سمت کلاینت) دوباره منتظر پیامک بماند.
        if (newPassword != confirmPassword)
            return new PasswordResetResult(PasswordResetStatus.PasswordMismatch);

        // کد اول مصرف می‌شود (تک‌مصرف): اگر ریست بعدش شکست بخورد، کاربر باید کد تازه بگیرد
        if (!await _otpService.VerifyOtpAsync(phoneNumber, code))
            return new PasswordResetResult(PasswordResetStatus.InvalidOtp);

        var user = await _userManager.FindByNameAsync(phoneNumber);
        if (user is null)
            return new PasswordResetResult(PasswordResetStatus.UserNotFound);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Password reset via OTP failed for {PhoneNumber}: {Errors}",
                PiiMask.Phone(phoneNumber), string.Join(" | ", result.Errors.Select(e => e.Description)));
            return new PasswordResetResult(PasswordResetStatus.ResetFailed, user.Id);
        }

        // رمز عوض شد: توکن‌های API قبلی دیگر معتبر نیستند (وگرنه دارندهٔ توکنِ
        // دزدیده‌شده با وجود ریست، تا ۱۸۰ روز دسترسی داشت). کوکی‌ها هم با تغییر
        // SecurityStamp در Identity حداکثر تا بازاعتبارسنجی بعدی می‌میرند.
        await _tokens.RevokeAllAsync(user.Id);

        return new PasswordResetResult(PasswordResetStatus.Success, user.Id);
    }
}