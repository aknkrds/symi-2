using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Symi.Api.Data;

namespace Symi.Api.Services;

public class ThumbnailWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ThumbnailWorker> _logger;

    public ThumbnailWorker(IServiceProvider services, ILogger<ThumbnailWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThumbnailWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pending = await db.MediaJobs.Where(m => m.Status == "pending").OrderBy(m => m.CreatedAt).Take(10).ToListAsync(stoppingToken);
                foreach (var job in pending)
                {
                    job.Status = "processing";
                    job.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);

                    // Simulate thumbnail generation (async) and mark completed
                    await Task.Delay(100, stoppingToken);
                    job.ThumbnailUrl = $"https://example.local/thumbnail/{Uri.EscapeDataString(job.ObjectKey)}";
                    job.Status = "completed";
                    job.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ThumbnailWorker loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
        _logger.LogInformation("ThumbnailWorker stopped");
    }
}