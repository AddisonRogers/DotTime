using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DTService;

public class ProcessInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string TimeStarted { get; set; }
    public required bool IsRunning { get; set; }
    public required string TimeEnded { get; set; }
    public required string Duration { get; set; }
    public required string Threads { get; set; }
    public required string MemoryUsage { get; set; }
}
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        File.Create("processes.json");

        var counter = 0;
        var processesDone = new List<ProcessInfo>();
        var processList = new HashSet<Process>();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            foreach (var process in Process.GetProcesses()) {
                if (!processList.Add(process)) continue; // Tries to add it to the hashset, if it fails it means it's already there
                try
                {
                    process.EnableRaisingEvents = true; // If it is not in the hashset, it means it's a new process, so we need to enable the event
                } catch (Exception error) { // This occurs when it is a system process
                    _logger.LogError("Error: {error}\n Process {process} cannot be logged :c", error.Message, process.ProcessName);
                } 
                
                process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
            }
            
            if (counter == 60) { // Every minute
                SendIt(processesDone); // Send the data to the API
                processesDone = null; // Clear the list
                counter = 0; 
            } else counter++;
            await Task.Delay(100, stoppingToken);
            continue;
            
            void ProcessExited(object? sender, EventArgs eventArgs, Process process)
            {
                Console.WriteLine("Process exited: {0}", process.ProcessName);
                var processInfo = new ProcessInfo {
                    Id = process.Id.ToString(),
                    Name = process.ProcessName,
                    TimeStarted = process.StartTime.ToString(), 
                    IsRunning = !process.HasExited,
                    TimeEnded = process.ExitTime.ToString(), 
                    Duration = (process.ExitTime - process.StartTime).TotalSeconds.ToString(), 
                    Threads = process.Threads.Count.ToString(),
                    MemoryUsage = process.PagedMemorySize64.ToString()
                };
                processesDone.Add(processInfo);
                processList.Remove(process);
            } 
        }
    }
    
    public void Update() // TODO
    {
        
    }

    private async void SendIt(List<ProcessInfo>? processes)
    {
        if (processes == null) return;
        
        // log data to a txt file
        var json = JsonSerializer.Serialize(processes);
        await File.AppendAllLinesAsync("processes.json", new[] { json });
    }
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        
        return base.StopAsync(cancellationToken);
    }
}