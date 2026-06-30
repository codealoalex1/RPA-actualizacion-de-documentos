# 1. ETAPA DE COMPILACIÓN (.NET 10.0 SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar archivos de proyectos y restaurar dependencias
COPY ["3_Aplicacion/Rpa.ServicioWindows/Rpa.ServicioWindows.csproj", "3_Aplicacion/Rpa.ServicioWindows/"]
COPY ["2_Infraestructura/Rpa.Infraestructura/Rpa.Infraestructura.csproj", "2_Infraestructura/Rpa.Infraestructura/"]
COPY ["1_Nucleo/Rpa.Nucleo/Rpa.Nucleo.csproj", "1_Nucleo/Rpa.Nucleo/"]
RUN dotnet restore "3_Aplicacion/Rpa.ServicioWindows/Rpa.ServicioWindows.csproj"

# Copiar todo el código y publicar el ejecutable en modo Release
COPY . .
WORKDIR "/src/3_Aplicacion/Rpa.ServicioWindows"
RUN dotnet publish "Rpa.ServicioWindows.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. ETAPA DE EJECUCIÓN (Sincronizada a la versión v1.61.0 requerida)
FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-noble AS final

# Copiamos el runtime de .NET 10 para actualizar el entorno de Playwright
COPY --from=mcr.microsoft.com/dotnet/runtime:10.0 /usr/share/dotnet /usr/share/dotnet

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Rpa.ServicioWindows.dll"]