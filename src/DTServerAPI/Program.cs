using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateSlimBuilder(args);
const string version = "1.0.0";
string? filePath;

var app = builder.Build();
IMongoCollection<BsonDocument> processCollection;

try
{
	filePath = Environment.GetEnvironmentVariable("UPDATE_KEY_FILE");
	if (filePath == null) throw new Exception("UPDATE_KEY_FILE environment variable not set.");
	
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
		var json = await JsonNode.ParseAsync(context.Request.Body);
		if (json == null || json["token"] == null || json["processes"] == null || json["processes"] is not JsonArray jsonProcesses) return Results.BadRequest();
		
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
			}; // God this is ugly

			await processCollection.InsertOneAsync(bsonDocument);
			Console.WriteLine("Inserted a document");
			return Result("Inserted");
		}

        List<Task> tasks = [];
		tasks.AddRange(jsonProcesses.Select(process => Task.Run(() =>
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

			var lastHistory = dbProcess["history"].AsBsonArray.LastOrDefault() as BsonDocument; // Get the last history entry

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
		})));

		try
		{
			await Task.WhenAll(tasks);
		}
		catch (Exception error)
		{
			Console.WriteLine(error);
			return Results.Problem("An error occurred:" + error.Message + error.StackTrace + "\nVersion " + version + "\n");
		}
		var update = Builders<BsonDocument>.Update
			.Set("processes", document["processes"]);
		var opts = new UpdateOptions
		{
			IsUpsert = true
		};
		
		await processCollection.UpdateOneAsync(filter, update, opts);
		Console.WriteLine("Updated a document");
		return Result("Updated");

		IResult Result(string message) => int.Parse(json["version"]?.ToString() ?? "0") < int.Parse(version) ? Results.Ok($"UPDATE + {message}") : Results.Ok($"{message} \nVersion: {version})");
	}
	catch (Exception ex)
	{
		// General error handling
		Console.WriteLine($"An error occurred: {ex.Message} {ex.StackTrace}");
		return Results.Problem("An error occurred:" + ex.Message + ex.StackTrace + "\nVersion " + version + "\n");
	}
});
app.MapGet("/update", (HttpContext context) =>
{
	Console.WriteLine("Received update request");
	var architecture = context.Request.Query["architecture"].ToString();
	using var memoryStream = new MemoryStream(File.ReadAllBytes(context.Request.Query["system"].ToString() switch
	{
		"windows" => architecture == "arm64" ? $@"{filePath}/release/win-arm64-{version}.exe" : $@"{filePath}/release/win-x64-{version}.exe",
		"linux" => architecture == "arm64" ? $@"{filePath}/release/linux-arm64-{version}" : $@"{filePath}/release/linux-x64-{version}",
		"macos" => architecture == "x64" ? $@"{filePath}/release/macos-x64-{version}.app" : $@"{filePath}/release/macos-arm64-{version}.app",
		_ => architecture == "arm64" ? $@"{filePath}/release/win-arm64-{version}.exe" : $@"{filePath}/release/win-x64-{version}.exe" // Windows is the default
	}));
	return memoryStream;
}); // Look into the podman compose file for the path
app.MapPost("/update", async delegate(HttpContext context) //TODO fix this lmao
{
	using var SKreader = new StreamReader(context.Request.Query["securityKey"].ToString());
	var body = await SKreader.ReadToEndAsync();
	SKreader.Dispose();

	if (!File.Exists($"{filePath}/sec/the_key.txt")) throw new FileNotFoundException($"The file {filePath} does not exist.");
	var securityKey = File.ReadAllText($"{filePath}/sec/the_key.txt");
	if (body != securityKey) Results.BadRequest("Invalid security key.");
	
	using var ms = new MemoryStream();
	await context.Request.Body.CopyToAsync(ms);
	await File.WriteAllBytesAsync($"{filePath}/release/{context.Request.Query["system"].ToString()}-{context.Request.Query["architecture"].ToString()}-{version}{context.Request.Query["extension"].ToString()}", ms.ToArray());
	ms.Dispose();
});

app.Run();