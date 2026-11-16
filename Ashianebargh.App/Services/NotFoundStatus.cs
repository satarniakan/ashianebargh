using Microsoft.AspNetCore.Http;

namespace Ashianebargh.App.Services;

/// <summary>
/// وضعیت ۴۰۴ را برای پاسخ‌های رندر استاتیک (SSR) ست می‌کند.
/// چرا انتساب مستقیم نه: Blazor وقتی وضعیت پیش از نوشتن پاسخ عوض شود، بدنهٔ رندرشده را
/// دور می‌ریزد و پاسخ ۴۰۴ با Content-Length: 0 برمی‌گردد (صفحهٔ ۴۰۴ نمایش داده نمی‌شود).
/// re-execute به /404 هم ممکن نیست، چون endpoint رازر کامپوننت در همان درخواست دوبار
/// اجرا می‌شود و با «'RemoteNavigationManager' already initialized» پاسخ ۵۰۰ می‌شود.
/// </summary>
public static class NotFoundStatus
{
    public static void Mark(HttpResponse? response)
    {
        if (response is null || response.HasStarted ||
            response.StatusCode == StatusCodes.Status404NotFound)
        {
            return;
        }

        // لحظهٔ نوشتن هدرها یعنی رندر تمام شده و بدنه در بافر است؛ تغییر وضعیت اینجا
        // هم ۴۰۴ واقعی می‌دهد و هم بدنهٔ صفحه را حفظ می‌کند.
        response.OnStarting(static state =>
        {
            ((HttpResponse)state).StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }, response);
    }
}
