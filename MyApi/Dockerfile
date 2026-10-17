# syntax=docker/dockerfile:1

# ---- Build stage: .NET 8 SDK image (compilers, NuGet, dotnet publish) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the NuGet layer is cached until MyApi.csproj changes.
COPY MyApi.csproj ./
RUN dotnet restore MyApi.csproj

COPY . ./
RUN dotnet publish MyApi.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---- Runtime stage: ASP.NET Core 8 runtime only (no SDK) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# The .NET 8 images listen on port 8080 and provide a non-root "app" user.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "MyApi.dll"]
