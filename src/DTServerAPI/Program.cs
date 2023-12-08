using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateSlimBuilder(args);

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

		var json = await JsonNode.ParseAsync(context.Request.Body);
		if (json == null || json["token"] == null || json["processes"] == null) return Results.BadRequest();
		
		if (json["processes"] is not JsonArray jsonProcesses) return Results.BadRequest();
		
		var filter = Builders<BsonDocument>.Filter.Eq("token", json["token"]!.ToString());
		var document = await processCollection.Find(filter).FirstOrDefaultAsync();
		
		if (document == null) { 
			Console.WriteLine("Inserting new document");

			var bsonDocument = new BsonDocument
			{
				{ "token", json["token"]?.ToString() },
				{ "processes", new BsonArray(
					jsonProcesses.Select(p => new BsonDocument
					{
						{ "name", p!["name"]?.ToString() },
						{ "history", new BsonArray(
							p["history"]?.AsArray().Select(h => new BsonDocument
							{
								{ "timeStarted", h!["timeStarted"]?.ToString() },
								{ "timeEnded", h["timeEnded"]?.ToString() }
							})
						)}
					}) 
				)}
			};

			await processCollection.InsertOneAsync(bsonDocument);
			return Results.Ok(); 
		}

		Parallel.ForEach(jsonProcesses, process =>
		{
			var dbProcess = document["processes"].AsBsonArray
				.Cast<BsonDocument>()
				.FirstOrDefault(p => p["name"].AsString == process?["name"]?.ToString());
			
			if (dbProcess == null) {
				var jsonObject = JsonDocument.Parse(process!.ToString()); 
				var bsonDocument = BsonDocument.Parse(jsonObject.RootElement.ToString());
				document["processes"].AsBsonArray.Add(bsonDocument);

				return;
			} // If the process doesn't exist, create a new one
			
			var lastHistory = dbProcess!["history"].AsBsonArray.LastOrDefault() as BsonDocument; // Get the last history entry
			
			if (lastHistory != null 
			    && process?["history"]?[0]?["timeStarted"] != null
			    && lastHistory.Contains("timeStarted")
			    && process["history"]?[0]?["timeStarted"]?.ToString() == lastHistory["timeStarted"].AsString
			    && lastHistory["timeEnded"].AsString == null)
			lastHistory["timeEnded"] = process["history"]?[0]?["timeEnded"]?.ToString();
			else {
				var jsonObject = JsonDocument.Parse(process?["history"]?[0]?.ToString()!); 
				var bsonDocument = BsonDocument.Parse(jsonObject.RootElement.ToString());
				dbProcess["history"].AsBsonArray.Add(bsonDocument);
			}
		});

		var update = Builders<BsonDocument>.Update
			.Set("processes", document["processes"]);
		var opts = new UpdateOptions
		{
			IsUpsert = true
		};
		
		_ = await processCollection.UpdateOneAsync(filter, update, opts);

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