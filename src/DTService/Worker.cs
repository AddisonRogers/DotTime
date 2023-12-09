using System.Diagnostics;
using System.Text.Json;

namespace DTService;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var counter = 0;
        var processList = new HashSet<Process>();
        var cache
        
        
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            Parallel.ForEach(Process.GetProcesses(), process =>
            {
                if (!processList.Add(process)) return; // Tries to add it to the hashset, if it fails it means it's already there
                try
                {
                    process.EnableRaisingEvents = true; // If it is not in the hashset, it means it's a new process, so we need to enable the event
                }
                catch (Exception error)
                {
                    // This occurs when it is a system process
                    logger.LogError("Error: {error}\n Process {process} cannot be logged :c", error.Message,
                        process.ProcessName);
                }

                process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
            });
            
            if (counter == 60) { // Every minute
                //
                counter = 0; 
            } else counter++;
            await Task.Delay(100, stoppingToken);
            continue;
            
            void ProcessExited(object? sender, EventArgs eventArgs, Process process)
            {
                if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Process {process} exited", process.ProcessName);
                processList.Remove(process);
                
            } 
        }
    }
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        return base.StopAsync(cancellationToken);
    }
}