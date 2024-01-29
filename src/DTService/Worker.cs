using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace DTService;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private const int Version = 1; // TODO update this on each release
    private const string Url = "http://localhost:5171"; // TODO update this if the server changes
    //private static HashSet<string> _ignoreList = null!; // TODO change this to a frozenSet
    private static readonly HttpClient Client = new();
    private static HashSet<Process> _processList = new();
    private static ConcurrentDictionary<string, List<ProcessHistory>> _cache = new();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var counter = 0;

        /*
        if (!File.Exists("ignoreList.json"))
        {
            logger.LogError("Error: {error}\n ignoreList.json not found, creating a new one");
            File.Create("ignoreList.json");
            _ignoreList = [];
        }
        else
        {
            var text = await File.ReadAllTextAsync("ignoreList.json", stoppingToken);
            if (text == "null")
            //_ignoreList = JsonSerializer.Deserialize<HashSet<string>>(text)!;
            logger.LogInformation("Deserialized ignoreList");
        }
        */

        List<Task> tasks = [];
        while (!stoppingToken.IsCancellationRequested)
        {
            //await Task.Delay(1000, stoppingToken);
            Thread.Sleep(1000);
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            var processArray = Process.GetProcesses();

            tasks.AddRange(processArray.Select(process =>
            {
                return !_processList.Add(process) ? Task.Run(() => ProcessHandler(process), stoppingToken) : null;
            })!);


            /*
            try { await Task.WhenAll(tasks); }
            catch (Exception error)
            {
                logger.LogError("Error: {error}\n {stackTrace}", error.Message, error.StackTrace);
            }
            */
            counter++;
            if (counter < (60 + Random.Shared.Next(0, 60))) continue;
            if (_cache.Keys.Count == 0) continue;
            counter = 0;
            
            await SendData(stoppingToken, _cache);
        }
    }

    private void ProcessHandler(Process process)
    {
        using (process)
        {
            //if (_ignoreList!.Contains(process.ProcessName)) return;
            if (!_processList.Add(process)) return;
            try
            {
                process.EnableRaisingEvents = true;
            }
            catch (Exception error) // This occurs when it is a system process
            {
                //logger.LogError("Error: {error}\n Process {process} cannot be logged :c", error.Message, process.ProcessName);
                //_ignoreList.Add(process.ProcessName); // TODO Still log via the backup method
                return;
            }


            process.Exited += (_, _) => ProcessExited(process, _cache, _processList); // TODO FIX
            process.WaitForExit();
        }
    }

    private async Task SendData(CancellationToken stoppingToken, ConcurrentDictionary<string, List<ProcessHistory>> cache)
    {
        try
        {
            await PingAndLogAsync();
        }
        catch (PingException e)
        {
            logger.LogError("Error due to server not up\nError: {error}\n {stackTrace}", e.Message, e.StackTrace);
        }
        catch (Exception e)
        {
            logger.LogError("Error: {error}\n {stackTrace}", e.Message, e.StackTrace);
        }
        var json = JsonSerializer.Serialize(cache);
        Console.WriteLine(json);
        await File.WriteAllTextAsync("ignoreList.json", json, stoppingToken); // TODO test
        var content = new StringContent(JsonSerializer.Serialize(cache), Encoding.UTF8, "application/json");
        Console.WriteLine(content);
        var response = await Client.PostAsync(Url + "/process", content, stoppingToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Cannot send data to the server. \nError: {error}\n {stackTrace}", response.StatusCode, response.ReasonPhrase);
            return;
        }
        if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Successfully sent data to server");
        
        cache.Clear();

        if (response.ReasonPhrase != null && response.ReasonPhrase[..6] == "UPDATE") await Update();
    }

    private async Task PingAndLogAsync()
    {
        Ping myPing = new();
        var reply = await myPing.SendPingAsync(Url, 1000);
        logger.LogInformation($"Status : {reply.Status}\nTime : {reply.RoundtripTime}\nAddress : {reply.Address}");
    }

    private void ProcessExited(Process process, ConcurrentDictionary<string, List<ProcessHistory>> cache, HashSet<Process> processList)
    {
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Process {process} exited", process.ProcessName);
                
        (cache.TryGetValue(process.ProcessName, out var value) 
                ? value 
                : cache[process.ProcessName] = new List<ProcessHistory>())
            .Add(new ProcessHistory(process.StartTime, process.ExitTime));

        processList.Remove(process);
        //process.EnableRaisingEvents = false;
    } // TODO FIX
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        //File.WriteAllTextAsync("ignoreList.json", JsonSerializer.Serialize(_ignoreList), cancellationToken);
        Client.Dispose();
        return base.StopAsync(cancellationToken);
    }
    
    private async Task Update()
    {
        using var httpResponse = await Client.GetAsync($"{Url}/update");
        await File.WriteAllBytesAsync($"DTService-{Version+1}-.exe", Convert.FromBase64String(await httpResponse.Content.ReadAsStringAsync()));
        Process.Start(new ProcessStartInfo
        {
            FileName = $"DTService-{Version+1}-.exe",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        });
        await StopAsync(new CancellationToken());
    }
}

public record ProcessHistory(DateTime StartTime, DateTime? EndTime);