using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Ashianebargh.App.Services;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون «۴۰۴ با بدنه»: انتساب مستقیم StatusCode در رندر استاتیک باعث می‌شد Blazor
/// بدنهٔ رندرشده را دور بریزد (پاسخ ۴۰۴ با Content-Length: 0)، و جایگزین
/// UseStatusCodePagesWithReExecute هم endpoint رازر کامپوننت را در همان درخواست دوبار
/// اجرا می‌کرد و پاسخ به ۵۰۰ با «'RemoteNavigationManager' already initialized» تبدیل
/// می‌شد. Mark وضعیت را به لحظهٔ نوشتن هدرها موکول می‌کند.
/// </summary>
public class NotFoundStatusTests
{
    [Fact]
    public void Mark_LeavesStatusUntouchedUntilHeadersAreWritten()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        NotFoundStatus.Mark(context.Response);

        // اگر همین‌جا ۴۰۴ ست شود، رندرِ باقی‌ماندهٔ صفحه دور ریخته می‌شود.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Mark_SetsNotFoundOnFlush_AndKeepsTheRenderedBody()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.MapGet("/product/ناموجود", async context =>
        {
            NotFoundStatus.Mark(context.Response);
            await context.Response.WriteAsync("<h1>صفحه یافت نشد</h1>");
        });

        await app.StartAsync();
        try
        {
            var response = await app.GetTestClient().GetAsync("/product/ناموجود");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("صفحه یافت نشد", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
