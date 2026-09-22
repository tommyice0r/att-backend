# Etapa 1: Compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar paquetes
COPY Licencia/ClientAccess.csproj ./Licencia/
RUN dotnet restore Licencia/ClientAccess.csproj

# Copiar código y compilar en modo Release
COPY Licencia/ ./Licencia/
WORKDIR /src/Licencia
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime ligero
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "ClientAccess.dll"]
