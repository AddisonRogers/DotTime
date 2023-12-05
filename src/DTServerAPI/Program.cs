using System.Text.Json.Nodes;
using MongoDB.Driver;
using MongoDB.Bson;

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
		if (json == null) return Results.BadRequest();
		
		if (json["processes"] is not JsonArray jsonProcesses) return Results.BadRequest();
		
		var filter = Builders<BsonDocument>.Filter.Eq("token", json["Token"]);
		
		var document = processCollection.Find(filter).FirstOrDefaultAsync().Result;
		if (document == null) { 
			Console.WriteLine("Inserting new document");
			await processCollection.InsertOneAsync(new Doc()
			{
				Token = json["Token"]?.ToString()!,
				Processes = jsonProcesses.Select(p => new Process()
				{
					Name = p!["Name"]?.ToString()!,
					History = p!["History"]?.AsArray().Select(h => new ProcessHistory()
					{
						TimeStarted = h!["TimeStarted"]?.ToString()!,
						TimeEnded = h["TimeEnded"]?.ToString()!
					}).ToArray()!
				}).ToArray()
			}.ToBsonDocument());
			return Results.Ok();
		} // If the document doesn't exist, create a new one TODO fix

		Parallel.ForEach(jsonProcesses, process =>
		{
			var dbProcess = document["processes"].AsBsonArray
				.Cast<BsonDocument>()
				.FirstOrDefault(p => p["name"].AsString == process?["name"]?.ToString());
			
			if (dbProcess == null) {
				document["processes"].AsBsonArray.Add(process.ToBsonDocument());
				return;
			} // If the process doesn't exist, create a new one
			
			var lastHistory = dbProcess["history"].AsBsonArray.LastOrDefault() as BsonDocument; // Get the last history entry
			
			if (lastHistory != null
			    && process?["History"]?[0]?["TimeStarted"]?.ToString() == lastHistory["timeStarted"].AsString
			    && lastHistory["timeEnded"].AsString == null) lastHistory["timeEnded"] = process?["History"]?[0]?["TimeEnded"]?.ToString();
			else dbProcess["history"].AsBsonArray.Add(process?["History"]?[0].ToBsonDocument());
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
	public required string Token { get; set; }
	public required Process[] Processes { get; set; }
};

public partial class Process
{
	public required string Name { get; set; }
	public required ProcessHistory[] History { get; set; }
};

public partial class ProcessHistory
{
	public required string TimeStarted { get; set; }
	public string? TimeEnded { get; set; }
};