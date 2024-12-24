# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /source
COPY --link ./src/Api/*.csproj .
RUN dotnet restore --arch x64
COPY --link ./src/Api .
RUN dotnet publish --os linux --arch x64 --no-restore -o /app


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled
# https://www.gnu.org/software/gettext/manual/html_node/Locale-Environment-Variables.html
ENV \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
WORKDIR /app
COPY --link --from=build /app .
ENTRYPOINT ["dotnet", "Api.dll"]