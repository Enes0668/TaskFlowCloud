# 1. Aşama (Build): Kodun derlenmesi için ağır .NET SDK imajını kullan
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce sadece .csproj dosyasını kopyala ve NuGet paketlerini indir (Docker Cache optimizasyonu)
COPY ["TaskFlow.Api.csproj", "./"]
RUN dotnet restore "TaskFlow.Api.csproj"

# Kalan tüm kodları kopyala ve Release modunda yayınla
COPY . .
RUN dotnet publish "TaskFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Aşama (Runtime): Canlıda çalışmak için sadece hafif ASP.NET Runtime imajını kullan
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Container Apps varsayılan olarak 8080 portundan trafik dinler
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]