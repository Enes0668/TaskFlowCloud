using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Tek bir ortak kimlik doğrulayıcı (Yerelde az login, Azure'da Managed Identity kullanır)
var credential = new DefaultAzureCredential();

// 2. Azure Key Vault'u Konfigürasyon Kaynağı Olarak Ekle (Şifreleri kasadan çeker)
var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"]!;
builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), credential);

// 3. PostgreSQL Veritabanını Bağla (Connection String doğrudan Key Vault'tan gelir)
var connectionString = builder.Configuration["DbConnectionString"];
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 4. Azure Blob Storage İstemcisini Şifresiz Bağla
var blobServiceUri = builder.Configuration["Azure:BlobServiceUri"]!;
builder.Services.AddSingleton(new BlobServiceClient(new Uri(blobServiceUri), credential));

// 5. Azure Service Bus İstemcisini Şifresiz Bağla
var sbNamespace = builder.Configuration["Azure:ServiceBusNamespace"]!;
builder.Services.AddSingleton(new ServiceBusClient(sbNamespace, credential));

// 6. Application Insights İzlemeyi Aç
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
//enes
// Uygulama başlarken veritabanı tablosu yoksa otomatik oluştur
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();