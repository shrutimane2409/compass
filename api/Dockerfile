FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Compass.Domain/Compass.Domain.csproj src/Compass.Domain/
COPY src/Compass.Api/Compass.Api.csproj src/Compass.Api/
RUN dotnet restore src/Compass.Api/Compass.Api.csproj
COPY src ./src
RUN dotnet publish src/Compass.Api/Compass.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
# Hosts such as Render pass the port in $PORT; default to 8080
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet Compass.Api.dll"]
