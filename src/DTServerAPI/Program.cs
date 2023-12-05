using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();
IMongoCollection<BsonDocument> processCollection;

try
{
	var gettingData = Task.Run(() =>
	{
		var client = new MongoClient("mongodb://localhost:27017");
		var database = client.GetDatabase("db");
		Console.WriteLine(database.ListCollections());
		return database.GetCollection<BsonDocument>("processes");
	});

	if (gettingData.Wait(5000)) processCollection = gettingData.Result;
	else throw new Exception("No response from database");
	Console.WriteLine("Connected to database");
}
catch (Exception ex)
{
	Console.WriteLine(ex);
	throw new Exception("Unable to connect to database.", ex);
}

app.MapGet("/", () => "Hello World!");
app.MapPost("/process", async delegate(HttpContext context)
{
	Console.WriteLine("Received request");
	try
	{
		Console.WriteLine("Received request");
		var version = context.Request.Headers["X-Api-Version"];
		Console.WriteLine($"Version: {version}");

		using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
		string requestContent;
		try
		{
			requestContent = await reader.ReadToEndAsync();
		}
		catch (IOException ex)
		{
			Console.WriteLine($"IO error occurred while reading the request: {ex.Message}");
			return Results.Problem();
		}

		Doc doc;
		
		try
		{
			doc = JsonSerializer.Deserialize<Doc>(requestContent, AppJsonSerializerContext.Default.Doc);
		}
		catch (JsonException ex)
		{
			Console.WriteLine($"JSON error occurred while deserializing the request content: {ex.Message}");
			return Results.BadRequest();
		}

		Console.WriteLine(doc.ToString());
		var filter = Builders<BsonDocument>.Filter.Eq("token", doc.Token); // Flag error but it works lmao
		
		var document = processCollection.Find(filter).FirstOrDefaultAsync().Result;
		if (document == null) { 
			Console.WriteLine("Inserting new document");
			await processCollection.InsertOneAsync(doc.ToBsonDocument());
			return Results.Ok();
		} // If the document doesn't exist, create a new one TODO fix

		Console.WriteLine(doc.Processes.Length);
		doc.Processes.AsParallel().ForAll(process =>
		{
			var dbProcess = document["processes"].AsBsonArray
				.Cast<BsonDocument>()
				.FirstOrDefault(p => p["name"].AsString == process.Name);

			if (dbProcess == null) {
				document["processes"].AsBsonArray.Add(process.ToBsonDocument());
				return;
			} // If the process doesn't exist, create a new one

			var lastHistory = dbProcess["history"].AsBsonArray.LastOrDefault() as BsonDocument; // Get the last history entry
			
			if (lastHistory != null
			    && process.History[0].TimeStarted == lastHistory["timeStarted"].AsString
			    && lastHistory["timeEnded"].AsString == null) lastHistory["timeEnded"] = process.History[0].TimeEnded;
			else dbProcess["history"].AsBsonArray.Add(process.History[0].ToBsonDocument());
		});

		var update = Builders<BsonDocument>.Update
			.Set("processes", document["processes"]);
		var opts = new UpdateOptions()
		{
			IsUpsert = true
		};
		
		var result = await processCollection.UpdateOneAsync(filter, update, opts);
		Console.WriteLine($"Updated {result.ModifiedCount} documents");
		
		return Results.Ok();
	}
	catch (Exception ex)
	{
		// General error handling
		Console.WriteLine($"An error occurred: {ex.Message} {ex.StackTrace}");
		return Results.Problem();
	}
});

app.Run();

public partial class Doc
{
	public string Token { get; set; }
	public Process[] Processes { get; set; }
};

public partial class Process
{
	public string Name { get; set; }
	public ProcessHistory[] History { get; set; }
};

public partial class ProcessHistory
{
	public string TimeStarted { get; set; }
	public string? TimeEnded { get; set; }
};

[JsonSerializable(typeof(Doc))]
[JsonSerializable(typeof(Process))]
[JsonSerializable(typeof(ProcessHistory))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}