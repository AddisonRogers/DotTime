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
    private HashSet<string> _ignoreList = null!; // TODO change this to a frozenSet
    private static readonly HttpClient Client = new();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var counter = 0;
        var processList = new ConcurrentDictionary<Process, bool>();
        var cache = new ConcurrentDictionary<string, List<ProcessHistory>>();
        
        try
        {
            if (!File.Exists("ignoreList.json")) throw new Exception("ignoreList.json does not exist");
            _ignoreList = JsonSerializer.Deserialize<HashSet<string>>(await File.ReadAllTextAsync("ignoreList.json", stoppingToken))!;
            Console.WriteLine("Loaded ignoreList.json");
        } 
        catch (Exception error)
        {
            logger.LogError("Error: {error}\n ignoreList.json not found, creating a new one", error.Message);
            File.Create("ignoreList.json");
            _ignoreList = [];
        }
        
        List<Task> tasks = [];
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            var processArray = Process.GetProcesses();
            tasks.AddRange(processArray.Select(process => Task.Run(() => // Replaced with an array
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
                try
                {
                    
                    Ping myPing = new();
                    var reply = await myPing.SendPingAsync(Url, 1000);
                    logger.LogInformation($"Status : {reply.Status}\nTime : {reply.RoundtripTime}\nAddress : {reply.Address}");
                    // Send the data 
                    var json = JsonSerializer.Serialize(cache);
                    await File.WriteAllTextAsync("ignoreList.json", json, stoppingToken); // TODO test
                    var content = new StringContent(JsonSerializer.Serialize(cache), Encoding.UTF8, "application/json");
                    var response = await Client.PostAsync(Url + "/process", content, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        if (logger.IsEnabled(LogLevel.Information))
                            logger.LogInformation("Successfully sent data to server");
                    }
                    else
                    {
                        logger.LogError("Error: {error}\n {stackTrace}", response.StatusCode, response.ReasonPhrase);
                    }

                    cache.Clear();

                    if (response.ReasonPhrase != null && response.ReasonPhrase[..6] == "UPDATE") await Update();
                }
                catch (PingException e)
                {
                    logger.LogError("Error sending the cache \n" +
                                    "This is due to the server being down briefly. If it keeps occuring please message dotracc\n" +
                                    "-> {error}\n {stackTrace}", e.Message, e.StackTrace);
                }
                
                catch (Exception e)
                {
                    logger.LogError("Error sending the cache \n" +
                                    "If it keeps occuring please message dotracc\n" +
                                    "-> {error}\n {stackTrace}", e.Message, e.StackTrace);
                }
                
                counter = 0;
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
    private async Task Update()
    {
        using var httpResponse = await Client.GetAsync($"{Url}/update");
        await File.WriteAllBytesAsync($"DTService-{Version+1}-.exe", Convert.FromBase64String(await httpResponse.Content.ReadAsStringAsync()));
        Process.Start(new ProcessStartInfo()
        {
            FileName = $"DTService-{Version+1}-.exe",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        });
        await StopAsync(new CancellationToken());
    }
}
public record ProcessHistory(DateTime StartTime, DateTime? EndTime);