namespace UpdaterService;

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
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            Update();
            await Task.Delay(86400000, stoppingToken);
        }
    }

    private static void Update()
    {
        var path = Directory.GetCurrentDirectory();
        if (File.Exists(path + "/TimeTrackService.exe")) File.Delete(path + "/TimeTrackService.exe");
        if (File.Exists(path + "/TimeTrack.exe")) File.Delete(path + "/TimeTrack.exe");
        // Now reinstall
        // RunScript()
        
    }
    
    public static void RunScript(string arg)
    {
        System.Diagnostics.Process process = new System.Diagnostics.Process();
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = $"/C {arg}";
        process.StartInfo = startInfo;
        process.Start();
        // curl -O https://github.com/aquasecurity/tfsec/releases/latest/download/tfsec-linux-amd64
    }
}