using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Symi.Api.Data;

namespace Symi.Api.Services;

public class PayoutWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PayoutWorker> _logger;

    public PayoutWorker(IServiceProvider services, ILogger<PayoutWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PayoutWorker started");
        var commissionRate = 0.25m;
        var vatRate = 0.20m; // örnek KDV
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Create plans for events ended 7+ days ago without plans
                var now = DateTime.UtcNow;
                var endedEventIds = await db.EventSessions
                    .GroupBy(s => s.EventId)
                    .Select(g => new { EventId = g.Key, LastEnd = g.Max(x => x.EndAt ?? x.StartAt) })
                    .Where(x => x.LastEnd < now.AddDays(-7))
                    .Select(x => x.EventId)
                    .ToListAsync(stoppingToken);

                foreach (var eid in endedEventIds)
                {
                    var hasPlan = await db.PayoutPlans.AnyAsync(p => p.EventId == eid, stoppingToken);
                    if (!hasPlan)
                    {
                        db.PayoutPlans.Add(new Models.PayoutPlan
                        {
                            EventId = eid,
                            Status = "pending",
                            ScheduledAt = now
                        });
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }

                // Process pending plans
                var pending = await db.PayoutPlans.Where(p => p.Status == "pending" && p.ScheduledAt <= now).ToListAsync(stoppingToken);
                foreach (var plan in pending)
                {
                    var gross = await db.Orders.Where(o => o.EventId == plan.EventId && o.Status == "paid").SumAsync(o => o.TotalAmount, stoppingToken);
                    var commission = gross * commissionRate;
                    var vat = commission * vatRate;
                    var net = gross - commission - vat;
                    plan.GrossAmount = gross;
                    plan.CommissionAmount = commission;
                    plan.VatAmount = vat;
                    plan.NetAmount = net;
                    plan.Status = "processed";
                    plan.ProcessedAt = now;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayoutWorker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        _logger.LogInformation("PayoutWorker stopped");
    }
}