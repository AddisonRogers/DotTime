using System.Diagnostics;

namespace DTService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            Process[] localAll = Process.GetProcesses();
            foreach (var process in localAll)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
            }

            await Task.Delay(1000, stoppingToken);
            continue;

            void ProcessExited(object? sender, EventArgs eventArgs, Process process)
            {
                
            } 
        }
    }
}()