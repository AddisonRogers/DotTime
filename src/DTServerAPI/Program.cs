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
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(nameof(DatabaseSettings)));

var app = builder.Build();

var client = new MongoClient(app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value.ConnectionString);
var database = client.GetDatabase(app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value.DatabaseName);
var processCollection = database.GetCollection<BsonDocument>(app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value.ProcessCollectionName);
Console.WriteLine("Connected to database");

app.MapGet("/", () => "Hello World!");
app.MapPost("/process", async delegate(HttpContext context)
{
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
			doc = JsonSerializer.Deserialize<Doc>(requestContent);
		}
		catch (JsonException ex)
		{
			Console.WriteLine($"JSON error occurred while deserializing the request content: {ex.Message}");
			return Results.BadRequest();
		}
		
		var filter = Builders<BsonDocument>.Filter.Eq("token", doc.token); // Flag error but it works lmao
		
		var document = processCollection.Find(filter).FirstOrDefaultAsync().Result;
		if (document == null) { 
			Console.WriteLine("Inserting new document");
			await processCollection.InsertOneAsync(doc.ToBsonDocument());
			return Results.Ok();
		} // If the document doesn't exist, create a new one TODO fix

		foreach (var process in doc.processes)
		{
			var dbProcess = document["processes"].AsBsonArray
				.Cast<BsonDocument>()
				.FirstOrDefault(p => p["name"].AsString == process.Name);

			if (dbProcess == null) {
				document["processes"].AsBsonArray.Add(process.ToBsonDocument()); 
				continue;
			} // If the process doesn't exist, create a new one

			var lastHistory = dbProcess["history"].AsBsonArray.Last() as BsonDocument; // Get the last history entry
			
			if (lastHistory != null
			    && process.History[0].TimeStarted == lastHistory["timeStarted"].AsString
			    && lastHistory["timeEnded"].AsString == null) lastHistory["timeEnded"] = process.History[0].TimeEnded;
			else dbProcess["history"].AsBsonArray.Add(process.History[0].ToBsonDocument());
		}
		
		
		/*			
					
					
					
					update := bson.D{{Key: "$push", Value: bson.D{{Key: "processes", Value: doc}}}}
				upsert := true
				after := options.After
				opts := options.FindOneAndUpdateOptions{
					ReturnDocument: &after,
					Upsert:         &upsert,
				}
				
				err = collection.FindOneAndUpdate(reqCtx, filter, update, &opts).Err()
				if err != nil {
					log.Fatal(err)
					return c.String(http.StatusInternalServerError, "There was a problem with the request.")
				}
					*/

	}
	catch (Exception ex)
	{
		// General error handling
		Console.WriteLine($"An error occurred: {ex.Message}");
	}
});

app.Run();

public struct Doc
{
	[property: JsonPropertyName("token")] 
	public string token { get; set; }
	[property: JsonPropertyName("processes")]
	public Process[] processes { get; set; }
};

public struct Process
{
	[property: JsonPropertyName("name")] 
	public string Name { get; set; }
	[property: JsonPropertyName("history")]
	public ProcessHistory[] History { get; set; }
};

public struct ProcessHistory
{
	[property: JsonPropertyName("timeStarted")]
	public string TimeStarted { get; set; }
	[property: JsonPropertyName("timeEnded")]
	public string? TimeEnded { get; set; }
};


[JsonSerializable(typeof(Doc))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

public class DatabaseSettings
{
	public string ConnectionString { get; set; } = null!;

	public string DatabaseName { get; set; } = null!;

	public string ProcessCollectionName { get; set; } = null!;
}