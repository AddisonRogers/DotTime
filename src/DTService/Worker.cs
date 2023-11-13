using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DTService;

public class ProcessInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string TimeStarted { get; set; }
    public bool IsRunning { get; set; }
    public string TimeEnded { get; set; }
    public string Duration { get; set; }
    public string Threads { get; set; }
    public string MemoryUsage { get; set; }
}
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int counter = 0;
        ProcessInfo[]? processes = { };
        var processList = new HashSet<Process>();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            foreach (var process in Process.GetProcesses())
            {
                if (!processList.Add(process)) continue;
                process.EnableRaisingEvents = true;
                process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
            }
            
            if (counter == 60)
            {
                SendIt(processes);
                processes = null;
                counter = 0;
            } else counter++;
            
            await Task.Delay(1000, stoppingToken);
            continue;


            Task ProcessExited(object? sender, EventArgs eventArgs, Process process)
            {
                var processInfo = new ProcessInfo
                {
                    Id = process.Id.ToString(),
                    Name = process.ProcessName,
                    TimeStarted = process.StartTime.ToString(), 
                    IsRunning = !process.HasExited,
                    TimeEnded = process.ExitTime.ToString(), 
                    Duration = (process.ExitTime - process.StartTime).TotalSeconds.ToString(), 
                    Threads = process.Threads.Count.ToString(),
                    MemoryUsage = process.PagedMemorySize64.ToString()
                };
                
                return Task.CompletedTask;
            } 
        }
    }
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        
        return base.StopAsync(cancellationToken);
    }

    public void Update()
    {
        
    }

    private async void SendIt(ProcessInfo[]? proccesses)
    {
        var json = JsonSerializer.Serialize(proccesses);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = new HttpClient();
        var response = await client.PostAsync("Your API Endpoint here", data);

        var result = await response.Content.ReadAsStringAsync();
        Console.WriteLine(result);
    }
}