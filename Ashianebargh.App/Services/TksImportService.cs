using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Ashianebargh.Application.Content;
using Ashianebargh.Application.Services;
using Ashianebargh.Domain.Content;
using Microsoft.Extensions.Logging;

namespace Ashianebargh.App.Services;

/// <summary>وارد کردن محصولات از tksterminal.com با استخراج HTML.</summary>
public sealed class TksImportService : ITksImportService
{
    private static readonly string SourceUrl = "https://tksterminal.com/";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IProductService _productService;
    private readonly IContentUnitOfWork _uow;
    private readonly ILogger<TksImportService> _logger;

    public TksImportService(
        IHttpClientFactory httpFactory,
        IProductService productService,
        IContentUnitOfWork uow,
        ILogger<TksImportService> logger)
    {
        _httpFactory = httpFactory;
        _productService = productService;
        _uow = uow;
        _logger = logger;
    }

    private HttpClient CreateClient() => _httpFactory.CreateClient("tksimport");

    public async Task<ImportResult> ImportProductsAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        int imported = 0, updated = 0;

        try
        {
            var client = CreateClient();
            var html = await client.GetStringAsync(SourceUrl, cancellationToken);
            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html);

            var productNames = ExtractProductNames(document);
            var defaultCategory = await GetOrCreateDefaultCategoryAsync();

            foreach (var name in productNames)
            {
                try
                {
                    var slug = SlugHelper.Normalize(name);
                    if (string.IsNullOrWhiteSpace(slug)) continue;

                    var existing = await _uow.Content.Products
                        .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

                    if (existing is not null)
                    {
                        existing.Name = name;
                        existing.Description = $"وارد شده از tksterminal.com — {name}";
                        await _uow.CompleteAsync();
                        updated++;
                    }
                    else
                    {
                        var dto = new ProductEditDto
                        {
                            Name = name,
                            Slug = slug,
                            Description = $"وارد شده از tksterminal.com — {name}",
                            CategoryId = defaultCategory.Id,
                            IsActive = true,
                            SortOrder = imported + updated
                        };
                        await _productService.SaveAsync(dto);
                        imported++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"خطا در وارد کردن «{name}»: {ex.Message}");
                    _logger.LogWarning(ex, "خطا در وارد کردن محصول {Name}", name);
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"خطا در اتصال به tksterminal.com: {ex.Message}");
            _logger.LogError(ex, "خطا در اتصال به tksterminal.com");
        }

        return new ImportResult(imported, updated, errors);
    }

    private static List<string> ExtractProductNames(IHtmlDocument document)
    {
        var names = new List<string>();
        var headings = document.QuerySelectorAll("h1, h2, h3, h4");
        foreach (var h in headings)
        {
            var text = h.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && IsProductName(text))
                names.Add(text);
        }

        var links = document.QuerySelectorAll("a[href]");
        foreach (var link in links)
        {
            var text = link.TextContent?.Trim();
            var href = link.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(text) && IsProductName(text) && IsProductUrl(href))
                names.Add(text);
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsProductName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length < 3 || text.Length > 100) return false;
        var skipWords = new[] { "home", "about", "contact", "products", "gallery", "certificates", "privacy", "cookie", "read more", "en", "türkçe" };
        var lower = text.ToLowerInvariant();
        if (skipWords.Any(s => lower.Contains(s))) return false;
        return true;
    }

    private static bool IsProductUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        var lower = href.ToLowerInvariant();
        return lower.Contains("product") || lower.Contains("terminal") || lower.Contains("connector") ||
               lower.Contains("plug") || lower.Contains("rcn") || lower.Contains("stamping") ||
               lower.Contains("injection") || lower.Contains("mould");
    }

    private async Task<Category> GetOrCreateDefaultCategoryAsync()
    {
        var category = await _uow.Content.Categories.FirstOrDefaultAsync(c => c.Slug == "imported-products");
        if (category is not null) return category;

        category = new Category
        {
            Name = "وارد شده از tksterminal.com",
            Slug = "imported-products",
            Description = "دسته‌بندی خودکار برای محصولات وارد شده از tksterminal.com",
            IsActive = true,
            SortOrder = 999
        };
        _uow.Content.Add(category);
        await _uow.CompleteAsync();
        return category;
    }
}