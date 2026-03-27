using MyCookbook.Data;

namespace MyCookbook.Services;

public class DailyLastCookedWorker : BackgroundService
{
    private static readonly TimeOnly TargetTime = new(1, 0, 0); // 1:00 AM daily

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyLastCookedWorker> _logger;

    public DailyLastCookedWorker(IServiceScopeFactory scopeFactory, ILogger<DailyLastCookedWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun();
            _logger.LogInformation("DailyLastCookedWorker: next run in {Delay}", delay);

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        _logger.LogInformation("DailyLastCookedWorker: processing date {Date}", yesterday);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CookbookDatabaseService>();
            await db.UpdateLastCookedForPlannedDateAsync(yesterday);
            _logger.LogInformation("DailyLastCookedWorker: completed for {Date}", yesterday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailyLastCookedWorker: failed for {Date}", yesterday);
        }
    }

    private static TimeSpan DelayUntilNextRun()
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var next = TargetTime.ToTimeSpan() > now.TimeOfDay
            ? today.ToDateTime(TargetTime)
            : today.AddDays(1).ToDateTime(TargetTime);
        return next - now;
    }
}
