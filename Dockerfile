# syntax=docker/dockerfile:1

# Étape 1 : image de base pour l'exécution
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Étape 2 : image SDK pour compiler et publier
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier d'abord le fichier projet pour profiter du cache Docker
COPY ["AkarangaBankMvc.Web.csproj", "./"]
RUN dotnet restore "AkarangaBankMvc.Web.csproj"

# Copier le reste du code source
COPY . .

# Publier l'application en mode Release
RUN dotnet publish "AkarangaBankMvc.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Étape 3 : image finale légère
FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AkarangaBankMvc.Web.dll"]