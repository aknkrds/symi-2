# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything and restore/publish
COPY ./ .
RUN dotnet restore symi.sln
RUN dotnet publish Symi.Api/Symi.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Default environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Default=Data Source=/app/data/symi.db
ENV ConnectionStrings__Redis=redis:6379
ENV Jwt__Issuer=symi
ENV Jwt__Audience=symi-client
ENV Jwt__AccessTokenSecret=CHANGE_ME_access_secret
ENV Jwt__RefreshTokenSecret=CHANGE_ME_refresh_secret

# Prepare data directory
RUN mkdir -p /app/data

EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Symi.Api.dll"]