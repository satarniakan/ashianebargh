using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ashianebargh.Application.Content;

namespace Ashianebargh.App.Endpoints;

/// <summary>
/// ساخت نقشهٔ سایت (sitemap.xml). جدا از endpoint نگه داشته شده تا بدون میزبان
/// وب و دیتابیس هم قابل تست باشد (escape شدن آدرس‌ها و حذف تکراری‌ها).
/// </summary>
public static class SitemapBuilder
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static string Build(string baseUrl, IEnumerable<SitemapEntryDto> entries)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("آدرس پایهٔ سایت خالی است.", nameof(baseUrl));

        var trimmedBase = baseUrl.TrimEnd('/');

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNs + "urlset",
                entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Path))
                    // یک مسیر نباید دو <url> بسازد (صفحهٔ پویا با slug برابر مسیر ثابت).
                    .GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Select(e => new XElement(SitemapNs + "url",
                        new XElement(SitemapNs + "loc", trimmedBase + e.Path),
                        new XElement(SitemapNs + "lastmod",
                            e.LastModified.ToUniversalTime().ToString("yyyy-MM-dd"))))));

        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var writer = new Utf8StringWriter();
        using (var xml = XmlWriter.Create(writer, settings))
        {
            document.Save(xml);
        }

        return writer.ToString();
    }

    /// <summary>StringWriter با اعلام UTF-8 تا XmlWriter اعلان درست بنویسد.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
