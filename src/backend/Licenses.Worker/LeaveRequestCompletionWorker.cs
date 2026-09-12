using Microsoft.Extensions.Options;

namespace Licenses.Worker;

public sealed class LeaveRequestCompletionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<LeaveCompletionOptions> options,
    ILogger<LeaveRequestCompletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Leave request completion worker is disabled.");
            return;
        }

        logger.LogInformation("Leave request completion worker is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ILeaveRequestCompletionProcessor>();
                await processor.ProcessBacklogAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Leave request completion polling iteration failed.");
            }

            await Task.Delay(options.Value.PollInterval, stoppingToken);
        }
    }
}
