using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IConfiguration _config;

    int a;
    public TasksController(
        AppDbContext db,
        BlobServiceClient blobServiceClient,
        ServiceBusClient serviceBusClient,
        IConfiguration config)
    {
        _db = db;
        _blobServiceClient = blobServiceClient;
        _serviceBusClient = serviceBusClient;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _db.Tasks.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] string title, [FromForm] string description, IFormFile? file)
    {
        string? fileUrl = null;

        // 1. Dosya varsa Azure Blob Storage'a yükle
        if (file is not null && file.Length > 0)
        {
            var containerName = _config["Azure:BlobContainerName"]!;
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobName = $"{Guid.NewGuid()}_{file.FileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, overwrite: true);
            fileUrl = blobClient.Uri.ToString();
        }

        // 2. Görevi PostgreSQL veritabanına kaydet
        var task = new TaskItem
        {
            Title = title,
            Description = description,
            AttachmentUrl = fileUrl
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        // 3. Asenkron bildirim için Azure Service Bus kuyruğuna mesaj bırak
        var queueName = _config["Azure:ServiceBusQueueName"]!;
        await using var sender = _serviceBusClient.CreateSender(queueName);
        var messagePayload = JsonSerializer.Serialize(new { task.Id, task.Title, task.CreatedAt });
        await sender.SendMessageAsync(new ServiceBusMessage(messagePayload));

        return CreatedAtAction(nameof(GetAll), new { id = task.Id }, task);
    }
}