# راهنمای عامل‌ها (AGENTS.md)

این ریپو یک **فریم‌ورک/قالب خالی فارسی راست‌چین** روی Blazor Server است، نه یک محصول دامنه‌دار.
هر تغییری باید پایه را عمومی و بدون دامنه نگه دارد؛ دامنهٔ مصرف‌کننده از نقاط توسعه وصل می‌شود.

## ابزار و ساختار

- SDK: طبق `global.json` برابر `10.0.100`؛ همهٔ پروژه‌ها `net10.0` با `Nullable` و `ImplicitUsings` فعال (`Directory.Build.props`).
- مدیریت نسخهٔ بسته‌ها مرکزی است: `Directory.Packages.props`. نسخهٔ خانوادهٔ EF/ASP.NET را هم‌سطح نگه دارید.
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
- برای هر اصلاح امنیتی/استقراری یک تست رگرسیون اضافه کنید (الگو: `ServiceRegistrationTests`).

## قراردادها و سبک

- کامنت‌ها و پیام‌های کاربر **فارسی** و راست‌چین؛ کامنت فقط «چرا» را بگوید نه «چه». متن‌های چاپی/کپی باید bidi-safe باشند.
- خطاها: سرویس‌ها `throw` می‌کنند؛ ترجمهٔ مرکزی در `ExceptionTranslator`/`AshianebarghExceptionHandler`. برای API از `ApiProblem` (بدنهٔ یکدست فارسی) استفاده کنید.
- پرداخت و چندمستأجری **حذف شده‌اند**؛ کد/جدول/مسیر مربوطه را برنگردانید.

## ناوردهای امنیتی (رگرسیون ممنوع)

- گاردهای استارتاپ Production در `Infrastructure/DependencyInjection.cs`: `Captcha:Provider=Turnstile` **و** هر دو کلید `Captcha:Turnstile:SiteKey/SecretKey` **و** `Security:Pepper` غیرخالی الزامی‌اند؛ نبود هر کدام باید استارتاپ را با پیام روشن متوقف کند (جلوگیری از بن‌بستِ «کلید خالی ← ورود ناممکن»).
- کوکی احراز هویت در Production `Secure=Always`؛ پس TLS واجب است. در compose تولیدی خاتمهٔ TLS با سرویس `proxy` (Caddy) انجام می‌شود و `ForwardedHeaders:TrustAllProxies` روشن است چون اپ فقط از داخل شبکه در دسترس است.
- هش OTP/توکن فقط به‌صورت هش + pepper ذخیره می‌شود؛ شمارنده‌های throttle اتمیک‌اند؛ مصرف OTP اتمیک است.
- لینک‌های ورودی کاربر پیش از ذخیره/رندر با `SafeUrl.Normalize` پالایش شوند؛ لاگ‌ها با `PiiMask` شماره/ایمیل را ماسک کنند.
- فرم‌های POST وب antiforgery دارند؛ fetchهای JS توکن را در هدر `X-XSRF-TOKEN` می‌فرستند. سیاست‌های نرخ‌سنجی (`otp-request`/`otp-verify`/`login`/`profile`) را حذف نکنید.

## استقرار و تنظیمات

- توسعه: `docker compose up --build` (پورت‌ها باز، رمز dev داخل فایل).
- تولید: `docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d` با `.env` کامل.
- متغیرهای اجباری تولید در `.env.example`: رمزهای SQL/Redis، `APP_ADMIN_PHONE`، `APP_DOMAIN`، `APP_TURNSTILE_SITE_KEY`، `APP_TURNSTILE_SECRET_KEY`، `APP_SECURITY_PEPPER`.
- در ادغام compose، `ports` الحاق می‌شود؛ برای بستن واقعی پورت از `ports: !reset []` استفاده کنید (نه `[]` خالی).
- `Ashianebargh.App/appsettings.json` و `.env` در `.gitignore`اند و هرگز commit نمی‌شوند؛ الگو `appsettings.json.example` است. مقدار مؤثر هر کلید عملیاتی: ردیف دیتابیس ← appsettings/env ← پیش‌فرض.