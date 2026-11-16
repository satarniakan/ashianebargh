# راهنمای عامل‌ها (AGENTS.md)

این ریپو یک **فریم‌ورک/قالب خالی فارسی راست‌چین** روی Blazor Server است، نه یک محصول دامنه‌دار.
هر تغییری باید پایه را عمومی و بدون دامنه نگه دارد؛ دامنهٔ مصرف‌کننده از نقاط توسعه وصل می‌شود.

## ابزار و ساختار

- SDK: طبق `global.json` برابر `10.0.100`؛ همهٔ پروژه‌ها `net10.0` با `Nullable` و `ImplicitUsings` فعال (`Directory.Build.props`).
- مدیریت نسخهٔ بسته‌ها مرکزی است: `Directory.Packages.props`. نسخهٔ خانوادهٔ EF/ASP.NET را هم‌سطح نگه دارید. پین گذرا (`CentralPackageTransitivePinningEnabled`) روشن است تا وصلهٔ آسیب‌پذیریِ گذرا بر نسخهٔ درخواستیِ والد غالب شود.
- منبع بسته‌ها: `NuGet.config` سطح ریپو تنها `nuget.org` را با `packageSourceMapping` تعریف می‌کند و منابع ماشین را پاک می‌کند (`<clear/>`) تا NU1507 نماند و زنجیرهٔ تأمین قابل‌اتکا باشد. mirror محلی را فقط با نقشهٔ بسته اضافه کنید، نه به‌صورت منبع نقشه‌نشده.
- قالب: `.template.config/template.json` با `shortName: empty-ashianebargh` و `sourceName: Ashianebargh` (و `preferNameDirectory`). محرمانهٔ محلی (`appsettings.json`)، کلیدهای DataProtection و خروجی build را exclude می‌کند. اگر نام/ساختار را عوض می‌کنید، قالب را هم به‌روز کنید.
- لایه‌ها: `Domain` (انتیتی‌ها/قراردادها) ← `Application` (سرویس‌ها/DTO) ← `Infrastructure` (EF/پیاده‌سازی) ← `App` (میزبان Blazor + endpointها + مایگریشن‌ها) و `Tests`.
- تنها پروژهٔ اجرایی `Ashianebargh.App` است؛ مایگریشن‌های EF عمداً در همین مونتاژ میزبان‌اند، نه در پایه.
- نقاط توسعهٔ دامنه: `IPermissionCatalog`، `ISettingCatalog`، `INavProvider`، `IAshianebarghModule`، `IFileAccessPolicy`. منطق دامنه را داخل پایه نریزید.

## ساخت و تست (هم‌راستا با CI)

```bash
dotnet build Ashianebargh.slnx -c Release
dotnet test  Ashianebargh.slnx -c Release
```

- تست رفت‌وبرگشت قالب (`template-roundtrip` در CI) مهم‌ترین تست ریپو است: خروجی `dotnet new empty-ashianebargh` باید خودش بسازد و سبز باشد. اگر قالب را تغییر می‌دهید، آن را هم بررسی کنید.
- تست‌ها تا جای ممکن به دیتابیس واقعی نیاز نداشته باشند؛ زمان از `TimeProvider` تزریق‌پذیر خوانده می‌شود تا انقضها/پنجره‌ها قابل تست باشند.
- برای هر اصلاح امنیتی/استقراری یک تست رگرسیون اضافه کنید (الگو: `ServiceRegistrationTests`، `ProductionHardeningTests`، `SupplyChainHardeningTests`).
- CI دروازهٔ بستهٔ آسیب‌پذیر دارد: هر هشدار `NU1901..NU1904` هنگام restore بیلد را قرمز می‌کند. بستهٔ آسیب‌پذیر را ارتقا دهید، نه اینکه دروازه را خاموش کنید.

## قراردادها و سبک

- کامنت‌ها و پیام‌های کاربر **فارسی** و راست‌چین؛ کامنت فقط «چرا» را بگوید نه «چه». متن‌های چاپی/کپی باید bidi-safe باشند.
- خطاها: سرویس‌ها `throw` می‌کنند؛ ترجمهٔ مرکزی در `ExceptionTranslator`/`AshianebarghExceptionHandler`. برای API از `ApiProblem` (بدنهٔ یکدست فارسی) استفاده کنید.
- پرداخت و چندمستأجری **حذف شده‌اند**؛ کد/جدول/مسیر مربوطه را برنگردانید.
- ۴۰۴: مسیر بی‌مطابقت را با `UseStatusCodePagesWithReExecute` به `/404` هدایت نکنید — endpoint رازر کامپوننت در همان درخواست دوبار اجرا می‌شود و پاسخ با «`RemoteNavigationManager` already initialized» به ۵۰۰ می‌رود. کد وضعیت را `NotFoundStatus.Mark` (App/Services) ست می‌کند که انتساب را به `OnStarting` موکول می‌کند؛ انتساب مستقیم `Response.StatusCode` در SSR باعث می‌شود Blazor بدنهٔ رندرشده را دور بریزد و ۴۰۴ با `Content-Length: 0` برگردد (نگهبان: `NotFoundStatusTests`).
- سربرگ سایت: سطر `.site-header-inner` تک‌سطریِ nowrap است، پس هر عنصر جا‌ن‌شده فشرده می‌شود و `.site-search` با `overflow:hidden` تا عرض صفر جمع و «ناپدید» می‌شود (با منوی پویای دیتابیس و برچسب‌های بلند فارسی رخ داد). `flex-shrink:0` روی جست‌وجو، دکمه‌های `site-cta`/`site-login`/`site-actions` (که `white-space:nowrap` هم دارند؛ وگرنه متن دکمه دوخطی می‌شود) و کلاس `site-header--stacked` (از `SiteHeaderLayout.IsMenuTooWideForOneRow`) منوی بلند را به سطر دوم می‌برد؛ اگر روزی برآورد خطا کرد، منو اسکرول افقی می‌کشد و جست‌وجو همان‌جا می‌ماند. برچسب منو در انگلیسی باید به `item.Title` برگردد (ترجمهٔ نبود لینک بی‌متن می‌سازد؛ `PageTranslations` خالی بود). همین برگشت برای عنوان/متن صفحات (`Page.razor`/`About.razor`) از `LocalizedText.Pick` انجام می‌شود؛ وگرنه صفحهٔ بی‌ترجمه در انگلیسی بی‌عنوان و خالی (مخفی) می‌شد. نگهبان: `SiteHeaderLayoutTests` و `LocalizedTextTests`.

## ناوردهای امنیتی (رگرسیون ممنوع)

- گاردهای استارتاپ Production در `Infrastructure/DependencyInjection.cs`: `Captcha:Provider=Turnstile` **و** هر دو کلید `Captcha:Turnstile:SiteKey/SecretKey` **و** `Security:Pepper` غیرخالی الزامی‌اند؛ نبود هر کدام باید استارتاپ را با پیام روشن متوقف کند (جلوگیری از بن‌بستِ «کلید خالی ← ورود ناممکن»).
- کوکی احراز هویت در Production `Secure=Always`؛ پس TLS واجب است. در compose تولیدی خاتمهٔ TLS با سرویس `proxy` (Caddy) انجام می‌شود و `ForwardedHeaders:TrustAllProxies` روشن است چون اپ فقط از داخل شبکه در دسترس است.
- هش OTP/توکن فقط به‌صورت هش + pepper ذخیره می‌شود؛ شمارنده‌های throttle اتمیک‌اند؛ مصرف OTP اتمیک است.
- لینک‌های ورودی کاربر پیش از ذخیره/رندر با `SafeUrl.Normalize` پالایش شوند؛ لاگ‌ها با `PiiMask` شماره/ایمیل را ماسک کنند.
- فرم‌های POST وب antiforgery دارند؛ fetchهای JS توکن را در هدر `X-XSRF-TOKEN` می‌فرستند. سیاست‌های نرخ‌سنجی (`otp-request`/`otp-verify`/`login`/`profile`) را حذف نکنید.
- نسخه‌های وصله: `System.Security.Cryptography.Pkcs` ≥ `10.0.13` و `AngleSharp` ≥ `1.5.0`. پایین‌تر بردن این‌ها آسیب‌پذیری شناخته‌شده (GHSA-846f-f764-76mx / GHSA-pgww-w46g-26qg) را برمی‌گرداند؛ تست `SupplyChainHardeningTests` نگهبان آن است.
- DataProtection در `App/DataProtectionSetup.cs` (قابل تست بدون میزبان): نام پایدار `Ashianebargh` + ماندگاری کلیدها روی ولوِل + رمزنگاری اختیاری با گواهی (`DataProtection:CertificatePath`). نام برنامه را عوض نکنید وگرنه کوکی/توکن ضدجعل بین نسخه‌ها می‌پرد.
- لاگ production: سطح `System`/`Microsoft` باید `Warning` بماند (`appsettings.Production.json`)؛ کلید API کاوه‌نگار داخل مسیر URL است و لاگ `HttpClient` در سطح Information آن را لو می‌دهد. خطای شبکهٔ کاوه‌نگار فقط «نوع خطا» را لاگ می‌کند، نه URL/کلید/پیام خام.
- Audit: رویدادهای کلیدی (`token.*`، `password.*`، `impersonation-end`) با `ActorId`/`IpAddress` و زمان `TimeProvider` ثبت می‌شوند. commit مستقلِ audit عمدی است تا با rollback تراکنش تجاری پاک نشود؛ آن را به commit تجاری گره نزنید.
- CI: `permissions: contents: read` (کمینه)، اکشن‌ها به SHA پین (تگ شناور ممنوع)، و پویش محرمانه با gitleaks. dependabot (nuget/github-actions/docker) SHAها و digestها را به‌روز نگه می‌دارد.

## استقرار و تنظیمات

- توسعه: `docker compose up --build` (پورت‌ها باز، رمز dev داخل فایل).
- دیتابیس محلی: روی این ماشین همهٔ دیتابیس‌های توسعه روی نمونهٔ `SQLEXPRESS` (TCP 1433) جمع شده‌اند و `appsettings.json` باید `Server=localhost,1433` بماند. نمونهٔ پیش‌فرض فقط با `lpc:` و از طریق Shared Memory در دسترس است؛ اگر جایی آن را دیدید، یعنی دیتابیس هنوز منتقل نشده است.
- تولید: `docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d` با `.env` کامل.
- متغیرهای اجباری تولید در `.env.example`: رمزهای SQL/Redis، `APP_ADMIN_PHONE`، `APP_DOMAIN`، `APP_TURNSTILE_SITE_KEY`، `APP_TURNSTILE_SECRET_KEY`، `APP_SECURITY_PEPPER`.
- در ادغام compose، `ports` الحاق می‌شود؛ برای بستن واقعی پورت از `ports: !reset []` استفاده کنید (نه `[]` خالی).
- `Ashianebargh.App/appsettings.json` و `.env` در `.gitignore`اند و هرگز commit نمی‌شوند؛ الگو `appsettings.json.example` است. مقدار مؤثر هر کلید عملیاتی: ردیف دیتابیس ← appsettings/env ← پیش‌فرض.
- `appsettings.Production.json` عمداً committed است و به ایمیج production می‌رود (برخلاف `appsettings.json` که هم gitignore و هم dockerignore است). هیچ محرمانه‌ای داخلش نگذارید؛ کلیدها/رمزها از env (compose) یا ردیف تنظیمات دیتابیس می‌آیند. `.dockerignore` فایل‌های محرمانه (`*.pfx`/`*.key`/`.env`/…) را از ایمیج حذف می‌کند.
- ایمیج پایهٔ `Dockerfile` به digest پین شده (`tag@sha256:`) تا registry آلوده نتواند محتوا را عوض کند؛ dependabot آن را به‌روز می‌کند. اپ با کاربر غیرریشه اجرا می‌شود و `HEALTHCHECK` روی `/health` دارد.
- compose تولیدی: شبکهٔ `backend` داخلی (`internal: true`)، `security_opt: no-new-privileges`، سقف لاگ json-file، healthcheck اپ و `depends_on: condition: service_healthy` برای proxy. اتصال SQL با `Encrypt=True`؛ `AllowedHosts` شامل `localhost` است تا healthcheck کانتینر رد نشود.