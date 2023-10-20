using System.Diagnostics;

namespace TimeTrack;

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
            //var processes = new Dictionary<string, _process>();
            foreach (var process in localAll)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
                
                //*
                //if (!processes.TryAdd(process.ProcessName, new _process(default, true, process.StartTime)))
                //{ // Activates when there is something already in there with the process name
                //    if (!processes.TryGetValue(process.ProcessName, out var val)) throw new Exception("idk message Addison");
                //    
                //    processes[process.ProcessName] = new _process(val.Uptime, isRunning: true, process.StartTime); 
                //    // So if we have the process in database we update it to make it so it is running
                //}; 
                //*
            }

            await Task.Delay(1000, stoppingToken);
            continue;

            void ProcessExited(object? sender, EventArgs eventArgs, Process process)
            {
                
                //if (!processes.TryGetValue(process.ProcessName, out var val)) throw new Exception("Failed to get process from dictionary");
                //processes[process.ProcessName] = new _process(val.Uptime + (Now - process.StartTime), isRunning: false, MinValue); 
                //*
                // When the process is exited then we flip the running value and update the uptime 
            }
        }
    }
}
public abstract class ProcessItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsRunning { get; set; }
    public List<ProcessRecord> History { get; set; }
}
public readonly record struct ProcessRecord(int StartTime, int EndTime)
{ 
    public int Uptime => EndTime - StartTime;
};