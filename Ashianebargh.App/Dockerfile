# ایمیج اجرایی میزبان. از ریشهٔ ریپو build می‌شود:
#   docker build -f Ashianebargh.App/Dockerfile -t ashianebargh-app .
# ایمیج‌های پایه به digest پین شده‌اند (تگ @sha256): تگ شناور است و یک registry آلوده
# می‌تواند محتوای آن را عوض کند؛ digest همان محتوای تأییدشده را قفل می‌کند. dependabot
# (ecosystem: docker) این digestها را هفتگی به‌روز می‌کند تا وصله‌های پایه از دست نرود.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY Ashianebargh.Domain/ ./Ashianebargh.Domain/
COPY Ashianebargh.Application/ ./Ashianebargh.Application/
COPY Ashianebargh.Infrastructure/ ./Ashianebargh.Infrastructure/
COPY Ashianebargh.App/ ./Ashianebargh.App/
RUN dotnet publish Ashianebargh.App/Ashianebargh.App.csproj -c Release -o /app/publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:11.0@sha256:bc0d6653979efba491ab3d6f589d691f96c309c3c3ef81325efa6978e7b7f242 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# اجرا با کاربر غیرریشه (app) — ایمیج‌های رسمی .NET متغیر APP_UID را دارند.
# curl برای HEALTHCHECK لازم است (ایمیج پایهٔ aspnet آن را ندارد).
# پوشه‌های نوشتنی (لاگ، کلیدهای DataProtection، فایل‌های بارگذاری‌شده) باید
# پیش از تغییر کاربر مالک داشته باشند، وگرنه اپ در Production خطای دسترسی می‌دهد.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/Logs /app/DataProtection-Keys /app/AppData \
    && chown -R $APP_UID:$APP_UID /app

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
# آمادگی اپ برای orchestrator/proxy؛ /health شامل بررسی دیتابیس هم هست.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Ashianebargh.App.dll"]
