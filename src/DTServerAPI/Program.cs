using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapPost("/process" ,  context => {
	Console.WriteLine("Received request");
	if (context.Response.ContentType == "application/json")
	{
		var doc = JsonSerializer.DeserializeAsync<Doc>(context.Request.Body, cancellationToken: context.RequestAborted);
		Console.WriteLine($"Received {doc.Result.ToString()}");
	}
	else
	{
		Console.WriteLine("Received non-json request");
		Console.WriteLine($"Received {context.Request.Body}");
	}
	return Task.FromResult(Results.Ok());
});

app.Run();

public struct Doc(
    [property: JsonPropertyName("token")] string token,
	[property: JsonPropertyName("processes")] Process[] processes
    );

public struct Process(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("history")] ProcessHistory[] History
	);

public struct ProcessHistory(
	[property: JsonPropertyName("timeStarted")] string TimeStarted,
	[property: JsonPropertyName("timeEnded")] string? TimeEnded
);



[JsonSerializable(typeof(Doc))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}