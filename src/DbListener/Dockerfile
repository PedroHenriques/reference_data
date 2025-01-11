# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /source
COPY --link ./src/DbListener/DbListener.csproj ./DbListener/DbListener.csproj
COPY --link ./src/SharedLibs/SharedLibs.csproj ./SharedLibs/SharedLibs.csproj
RUN dotnet restore ./DbListener/DbListener.csproj --arch x64
RUN dotnet restore ./SharedLibs/SharedLibs.csproj --arch x64
COPY --link ./src/DbListener ./DbListener/
COPY --link ./src/SharedLibs ./SharedLibs/
RUN dotnet publish ./DbListener/DbListener.csproj --os linux --arch x64 --no-restore -o /app


# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled
# https://www.gnu.org/software/gettext/manual/html_node/Locale-Environment-Variables.html
ENV \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
WORKDIR /app
COPY --link --from=build /app .
ENTRYPOINT ["dotnet", "DbListener.dll"]