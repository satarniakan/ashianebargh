namespace Ashianebargh.Domain.Content;

/// <summary>کلیدهای تنظیمات اسلایدر صفحهٔ اول؛ ردیف دیتابیس ← appsettings ← پیش‌فرض.</summary>
public static class SliderSettingKeys
{
    /// <summary>انیمیشن تعویض اسلاید: fade | slide | none.</summary>
    public const string Transition = "slider:transition";

    /// <summary>مدت نمایش هر اسلاید به ثانیه.</summary>
    public const string IntervalSeconds = "slider:interval-seconds";

    /// <summary>نمایش کل اسلایدر در صفحهٔ اول؛ خاموش باشد بخش معرفی (hero) جای آن می‌آید.</summary>
    public const string Enabled = "slider:enabled";

    public const string DefaultTransition = "fade";
    public const int DefaultIntervalSeconds = 5;
    public const bool DefaultEnabled = true;
    public const int MinIntervalSeconds = 2;
    public const int MaxIntervalSeconds = 60;

    public static readonly string[] Transitions = ["fade", "slide", "none"];
}