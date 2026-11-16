using Ashianebargh.App.Services;

namespace Ashianebargh.Tests;

/// <summary>
/// رگرسیون «با تغییر زبان بعضی صفحات/لینک‌های منو مخفی می‌شوند». وقتی برای صفحه‌ای
/// ترجمهٔ انگلیسی ثبت نشده باشد، کد قدیم مقدار null را مستقیم رندر می‌کرد و صفحه
/// در انگلیسی بی‌عنوان و بدون متن می‌شد؛ حالا به متن پایهٔ فارسی برمی‌گردد.
/// </summary>
public class LocalizedTextTests
{
    [Fact]
    public void English_WithTranslation_UsesTranslation() =>
        Assert.Equal("Warranty", LocalizedText.Pick(true, "Warranty", "گارانتی"));

    [Fact]
    public void English_WithoutTranslation_FallsBackToBaseText() =>
        Assert.Equal("گارانتی", LocalizedText.Pick(true, null, "گارانتی"));

    [Fact]
    public void English_WithBlankTranslation_FallsBackToBaseText() =>
        Assert.Equal("گارانتی", LocalizedText.Pick(true, "   ", "گارانتی"));

    [Fact]
    public void Persian_IgnoresTranslationAndUsesBaseText() =>
        Assert.Equal("گارانتی", LocalizedText.Pick(false, "Warranty", "گارانتی"));

    [Fact]
    public void BothMissing_ReturnsEmptyInsteadOfNull() =>
        Assert.Equal(string.Empty, LocalizedText.Pick(true, null, null));
}
