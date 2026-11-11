using Ashianebargh.Application.DTOs;

namespace Ashianebargh.Application.Content;

/// <summary>وارد کردن محصولات از سایت tksterminal.com (به‌خاطر عدم وجود API، از scraping HTML استفاده می‌شود).</summary>
public interface ITksImportService
{
    /// <summary>کشورده و وارد کردن محصولات. بازگشت: تعداد محصولات جدید/به‌روزرسانی شده.</summary>
    Task<ImportResult> ImportProductsAsync(CancellationToken cancellationToken = default);
}

public sealed record ImportResult(int Imported, int Updated, List<string> Errors);
