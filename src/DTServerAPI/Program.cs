using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateSlimBuilder(args);
const int version = 1;

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
        var json = await JsonNode.ParseAsync(context.Request.Body);
        if (json == null || json["token"] == null || json["processes"] == null ||
            json["processes"] is not JsonArray jsonProcesses) return Results.BadRequest();

        var filter = Builders<BsonDocument>.Filter.Eq("token", json["token"]!.ToString());
        var document = await processCollection.Find(filter).FirstOrDefaultAsync();

        if (document == null)
        {
            Console.WriteLine("Inserting new document");

            var bsonDocument = new BsonDocument
            {
                { "token", json["token"]?.ToString() },
                {
                    "processes", new BsonArray(
                        jsonProcesses.Select(p => new BsonDocument
                        {
                            { "name", p!["name"]?.ToString() },
                            {
                                "history", new BsonArray(
                                    p["history"]?.AsArray().Select(h => new BsonDocument
                                    {
                                        { "timeStarted", h!["timeStarted"]?.ToString() },
                                        { "timeEnded", h["timeEnded"]?.ToString() }
                                    })
                                )
                            }
                        })
                    )
                }
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

            if (dbProcess == null)
            {
                var jsonObject = JsonDocument.Parse(process!.ToString());
                var bsonDocument = BsonDocument.Parse(jsonObject.RootElement.ToString());
                document["processes"].AsBsonArray.Add(bsonDocument);

                return;
            } // If the process doesn't exist, create a new one

            var lastHistory =
                dbProcess["history"].AsBsonArray.LastOrDefault() as BsonDocument; // Get the last history entry

            if (lastHistory != null
                && process?["history"]?[0]?["timeStarted"] != null
                && lastHistory.Contains("timeStarted")
                && process["history"]?[0]?["timeStarted"]?.ToString() == lastHistory["timeStarted"].AsString
                && lastHistory["timeEnded"].AsString == null)
            {
                lastHistory["timeEnded"] = process["history"]?[0]?["timeEnded"]?.ToString();
            }
            else
            {
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

        IResult Result(string message)
        {
            return int.Parse(json["version"]?.ToString() ?? "0") < version
                ? Results.Ok($"UPDATE + {message}")
                : Results.Ok($"{message} \nVersion: {version})");
        }
    }
    catch (Exception ex)
    {
        // General error handling
        Console.WriteLine($"An error occurred: {ex.Message} {ex.StackTrace}");
        return Results.Problem("An error occurred:" + ex.Message + ex.StackTrace + "\nVersion " + version + "\n");
    }
});
app.MapGet("/update", async delegate(HttpContext context)
{
    Console.WriteLine("Received update request");

    var architecture = string.IsNullOrEmpty(context.Request.Query["architecture"].ToString()) 
        ? "x86_64" 
        : context.Request.Query["architecture"].ToString();    
    
    var system = string.IsNullOrEmpty(context.Request.Query["system"].ToString()) 
        ? "windows" 
        : context.Request.Query["system"].ToString();    
    
    return Convert.ToBase64String(await File.ReadAllBytesAsync(Environment.CurrentDirectory + $"/files/{system}-{architecture}.bytes"));
});

app.Run();