# ==========================
# BUILD STAGE
# ==========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o /app

# ==========================
# RUNTIME STAGE
# ==========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app .

EXPOSE 5000

ENTRYPOINT ["dotnet", "BlogApp.dll"]
