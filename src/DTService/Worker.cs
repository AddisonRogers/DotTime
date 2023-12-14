using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace DTService;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private HashSet<string> _ignoreList = null!;
    private static readonly HttpClient Client = new();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var counter = 0;
        var processList = new ConcurrentDictionary<Process, bool>();
        
        var cache = new ConcurrentDictionary<string, List<ProcessHistory>>();
        
        try
        {
            _ignoreList = JsonSerializer.Deserialize<HashSet<string>>(await File.ReadAllTextAsync("ignoreList.json", stoppingToken))!;
        } catch (Exception error)
        {
            logger.LogError("Error: {error}\n ignoreList.json not found, creating a new one", error.Message);
            _ignoreList = [];
        }
        
        List<Task> tasks = [];
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            tasks.AddRange(Process.GetProcesses().Select(process => Task.Run(() =>
            {
                using (process)
                {
                    if (_ignoreList!.Contains(process.ProcessName)) return;
                    if (!processList.TryAdd(process, true)) return;
                    try
                    {
                        process.EnableRaisingEvents = true;
                    }
                    catch (Exception error) // This occurs when it is a system process
                    {
                        logger.LogError("Error: {error}\n Process {process} cannot be logged :c", error.Message, process.ProcessName);
                        _ignoreList.Add(process.ProcessName);
                        return;
                    }
                    process.Exited += (_, _) => ProcessExited(process);
                }
                Task.Delay(Random.Shared.Next(100), stoppingToken);
            }, stoppingToken)));
            
            try { await Task.WhenAll(tasks); }
            catch (Exception error)
            {
                logger.LogError("Error: {error}\n {stackTrace}", error.Message, error.StackTrace);
            }

            if (counter == 60 + Random.Shared.Next(0, 60))
            {
                await File.WriteAllTextAsync("ignoreList.json", JsonSerializer.Serialize(_ignoreList), stoppingToken);
                counter = 0;
                cache.Clear();
            } else counter++;
            
            await Task.Delay(1000, stoppingToken);
            continue;

            void ProcessExited(Process process)
            {
                if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Process {process} exited", process.ProcessName);
                
                (cache.TryGetValue(process.ProcessName, out var value) 
                        ? value 
                        : cache[process.ProcessName] = [])
                        .Add(new ProcessHistory(process.StartTime, process.ExitTime));
                
                processList.TryRemove(process, out _);
                process.EnableRaisingEvents = false;
            } 
        }
    }
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        File.WriteAllTextAsync("ignoreList.json", JsonSerializer.Serialize(_ignoreList), cancellationToken);
        Client.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
public record ProcessHistory(DateTime StartTime, DateTime? EndTime);