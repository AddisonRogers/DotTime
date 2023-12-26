using System.Text;
using System.Text.Json;

namespace DTAPI_Testing;

public class APITests
{
    private readonly HttpClient _client = new();
    private const string BaseUrl = "http://localhost:5171";

    [Fact]
    public async Task GetRoot() => (await _client.GetAsync(BaseUrl)).EnsureSuccessStatusCode();
    
    [Fact]
    public async Task PostStartProcess()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/process")
        {
                Headers =
                {
                    { "X-Api-Version", "1.0.0" },
                    { "Content-Type", "application/json" }
                },
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    token = "xxxxx",
                    processes = new[]
                    {
                        new
                        {
                            name = "test3",
                            history = new[]
                            {
                                new
                                {
                                    timeStarted = "2022-01-01 00:00:00",
                                    timeEnded = ""
                                }
                            }
                        }
                    }
                }), Encoding.UTF8, "application/json")
            };

        (await _client.SendAsync(request)).EnsureSuccessStatusCode();
    }
    
    [Fact]
    public async Task PostEndProcess()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/process")
        {
                Headers =
                {
                    { "X-Api-Version", "1.0.0" },
                    { "Content-Type", "application/json" }
                },
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    token = "xxxxx",
                    processes = new[]
                    {
                        new
                        {
                            name = "test3",
                            history = new[]
                            {
                                new
                                {
                                    timeStarted = "2022-01-01 00:00:00",
                                    timeEnded = "2022-01-01 00:00:00"
                                }
                            }
                        }
                    }
                }), Encoding.UTF8, "application/json")
            };

        (await _client.SendAsync(request)).EnsureSuccessStatusCode();
    }
    
    [Fact]
    public async Task PostMultipleProcesses()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/process")
        {
            Headers =
            {
                { "X-Api-Version", "1.0.0" },
                { "Content-Type", "application/json" }
            },
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                token = "xxxxx",
                version = "1.0.0",
                processes = new[]
                {
                    new
                    {
                        name = "test3",
                        history = new[]
                        {
                            new
                            {
                                timeStarted = "2022-01-01 00:00:00",
                                timeEnded = "2022-01-01 00:00:00"
                            }
                        }
                    },
                    new
                    {
                        name = "test4",
                        history = new[]
                        {
                            new
                            {
                                timeStarted = "2022-01-02 00:00:00",
                                timeEnded = "2022-01-03 00:00:00"
                            },
                            new
                            {
                                timeStarted = "2022-01-02 00:00:00",
                                timeEnded = "2022-01-03 00:00:00"
                            },
                            new
                            {
                                timeStarted = "2022-01-02 00:00:00",
                                timeEnded = ""
                            }
                        }
                    }
                }
            }), Encoding.UTF8, "application/json")
        };

        (await _client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetUpdate()
    {
        await File.WriteAllBytesAsync("./UpdatedFiles", await (await _client.GetAsync(BaseUrl + "/update")).Content.ReadAsByteArrayAsync());
        Assert.True(File.Exists("./UpdatedFiles"));
    }
}

