# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /source
COPY --link ./setup/local/webhook/webhook.csproj ./webhook/webhook.csproj
RUN dotnet restore ./webhook/webhook.csproj --arch x64
COPY --link ./setup/local/webhook ./webhook/
RUN dotnet publish ./webhook/webhook.csproj --os linux --arch x64 --no-restore -o /app


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled
# https://www.gnu.org/software/gettext/manual/html_node/Locale-Environment-Variables.html
ENV \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
WORKDIR /app
COPY --link --from=build /app .
ENTRYPOINT ["dotnet", "webhook.dll"]