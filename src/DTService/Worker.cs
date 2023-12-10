using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DTService;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private List<string> _ignoreList = null!;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var counter = 0;
        var processList = new ConcurrentDictionary<Process, bool>();
        
        var cache = new ConcurrentDictionary<string, List<ProcessHistory>>();
        
        try
        {
            _ignoreList = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync("ignoreList.json", stoppingToken))!;
        } catch (Exception error)
        {
            logger.LogError("Error: {error}\n ignoreList.json not found, creating a new one", error.Message);
            _ignoreList = [];
        }
        
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            
            Parallel.ForEach(Process.GetProcesses(), process =>
            {
                if (!processList.TryAdd(process, true)) return; 
                try
                {
                    if (_ignoreList!.Contains(process.ProcessName)) return; 
                    process.EnableRaisingEvents = true; 
                }
                catch (Exception error)
                {
                    // This occurs when it is a system process
                    logger.LogError("Error: {error}\n Process {process} cannot be logged :c", error.Message,process.ProcessName);
                    _ignoreList!.Add(process.ProcessName);
                    return;
                }

                process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
            });
            
            if (counter == 60)
            {
                // Every Minute, send the cache to the server
                var json = JsonSerializer.Serialize(cache);
                var url = Environment.GetEnvironmentVariable("DT_URL") ?? throw new Exception("DT_URL not found");
                var response = await new HttpClient().PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), stoppingToken);
                
                if (response.IsSuccessStatusCode) logger.LogInformation("Successfully sent data to server");
                else logger.LogError("Error: {error}\n {response}", response.StatusCode, await response.Content.ReadAsStringAsync(stoppingToken));
                
                counter = 0;
                cache.Clear();
            } else counter++;
            
            
            await Task.Delay(100, stoppingToken);
            continue;
            
            void ProcessExited(object? sender, EventArgs eventArgs, Process process)
            {
                if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Process {process} exited", process.ProcessName);
                
                (cache.TryGetValue(process.ProcessName, out var value) 
                        ? value 
                        : cache[process.ProcessName] = [])
                        .Add(new ProcessHistory(process.StartTime, process.ExitTime));
                
                processList.TryRemove(process, out _);
            } 
        }
    }
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        File.WriteAllTextAsync("ignoreList.json", JsonSerializer.Serialize(_ignoreList), cancellationToken);
        return base.StopAsync(cancellationToken);
    }
}
public record ProcessHistory(DateTime StartTime, DateTime EndTime);