using System.ComponentModel;
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
			
		var document = processCollection.Find(filter).FirstOrDefaultAsync();
		var documentResult = document.Result;
		if (documentResult == null) { 
			Console.WriteLine("Inserting new document");
			await processCollection.InsertOneAsync(doc.ToBsonDocument());
			return Results.Ok();
		} // If the document doesn't exist, create a new one

		foreach (var process in doc.processes)
		{
			// direct conversion 
			bool matchFound = false;
			for (int i = 0; i < documentResult["processes"].AsBsonArray.Count; i++)
			{
				var existingProcess = documentResult["processes"].AsBsonArray[i];
				if (process.Name != existingProcess["name"].AsString)
				{
					continue;
				}
				// We found a matching process
				if (process.History[0].TimeStarted == existingProcess["history"].AsBsonArray[^1]["timeStarted"].AsString && existingProcess["history"].AsBsonArray[^1]["timeEnded"].AsString == null)
				{
					existingProcess["history"].AsBsonArray[^1]["timeEnded"] = process.History[0].TimeEnded;
					matchFound = true;
					break;
				}
				existingProcess["history"].AsBsonArray.Add(process.History[0].ToBsonDocument());
				matchFound = true;
			}
		}
		
		/*			
					matchFound := false
						for i, existingProcess := range existingDoc.Processes {
							if process.Name != existingProcess.Name {
								continue
							}
							// We found a matching process
							if (process.History.TimeStarted == existingProcess.History[len(existingProcess.History)-1].TimeStarted) && (existingProcess.History[len(existingProcess.History)-1].TimeEnded == nil) {
								existingDoc.Processes[i].History[len(existingProcess.History)-1].TimeEnded = process.History.TimeEnded
								matchFound = true
								break
							}
							existingDoc.Processes[i].History = append(existingProcess.History, process.History)
							matchFound = true
						}

						// If no matching process was found, add a new process.
						if !matchFound {
							existingDoc.Processes = append(existingDoc.Processes, ProcessDB{
								Name:    process.Name,
								History: []ProcessHistory{process.History},
							})
						}
					}
					
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