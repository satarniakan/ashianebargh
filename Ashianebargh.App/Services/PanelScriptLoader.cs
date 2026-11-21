using Microsoft.JSInterop;

namespace Ashianebargh.App.Services;

/// <summary>
/// بارگذاری تنبل اسکریپت‌های پنل. ناوبری افزانهٔ Blazor اسکریپت‌های لایوت صفحهٔ مقصد را
/// اجرا نمی‌کند؛ پس وقتی کاربر از سایت عمومی وارد پنل می‌شود window.priceInput تعریف
/// نشده و فرم‌ها هنگام attach می‌شکنند. واردسازی فایل به‌صورت ماژول از خود کامپوننت،
/// مستقل از لایوت، آن را تضمین می‌کند (ریجِستری ماژول، اجرای دوم را حذف می‌کند).
/// </summary>
public sealed class PanelScriptLoader(IJSRuntime js)
{
    private Task<IJSObjectReference>? _priceInput;

    /// <summary>priceInput.js را حداکثر یک‌بار در این scope (مدار) اجرا می‌کند.</summary>
    public Task<IJSObjectReference> PriceInputAsync() =>
        _priceInput ??= js.InvokeAsync<IJSObjectReference>("import", "/priceInput.js").AsTask();
}
