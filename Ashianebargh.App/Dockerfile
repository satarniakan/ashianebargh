# ایمیج اجرایی میزبان. از ریشهٔ ریپو build می‌شود:
#   docker build -f Ashianebargh.App/Dockerfile -t ashianebargh-app .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY Ashianebargh.Domain/ ./Ashianebargh.Domain/
COPY Ashianebargh.Application/ ./Ashianebargh.Application/
COPY Ashianebargh.Infrastructure/ ./Ashianebargh.Infrastructure/
COPY Ashianebargh.App/ ./Ashianebargh.App/
RUN dotnet publish Ashianebargh.App/Ashianebargh.App.csproj -c Release -o /app/publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# اجرا با کاربر غیرریشه (app) — ایمیج‌های رسمی .NET متغیر APP_UID را دارند.
# پوشه‌های نوشتنی (لاگ، کلیدهای DataProtection، فایل‌های بارگذاری‌شده) باید
# پیش از تغییر کاربر مالک داشته باشند، وگرنه اپ در Production خطای دسترسی می‌دهد.
RUN mkdir -p /app/Logs /app/DataProtection-Keys /app/AppData \
    && chown -R $APP_UID:$APP_UID /app

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Ashianebargh.App.dll"]
