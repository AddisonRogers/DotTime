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
app.MapPost("/process" ,  async delegate(HttpContext context) {
	Console.WriteLine("Received request");
	var version = context.Request.Headers["X-Api-Version"];
	Console.WriteLine($"Version: {version}");
	using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
	var doc = JsonSerializer.Deserialize<Doc>(await reader.ReadToEndAsync());
	// then we store it in mongo
	
	// filter := bson.D{{Key: "token", Value: doc.Token}}
	// update := bson.D{{Key: "$push", Value: bson.D{{Key: "processes", Value: doc.Processes}}}}
	/*doc := new(Doc)

		if err = c.Bind(doc); err != nil {
			return echo.NewHTTPError(http.StatusBadRequest, "Invalid format")
		}

		filter := bson.D{{Key: "token", Value: doc.Token}}

		var existingDoc DocDB
		err = collection.FindOne(reqCtx, filter).Decode(&existingDoc)
		if errors.Is(mongo.ErrNoDocuments, err) {
			// Insert the new document if it doesn't exist
			_, err = collection.InsertOne(reqCtx, doc)
			if err != nil {
				log.Fatal(err)
				return c.String(http.StatusInternalServerError, "There was a problem with request.")
			}
			fmt.Println("Request success.")
			return c.String(http.StatusOK, "Request success.")
		} else if err != nil {
			log.Fatal(err)
			return c.String(http.StatusInternalServerError, "There was a problem with the request.")
		} else {
			// We found an existing document
			for _, process := range doc.Processes { // For all the processes that has been sent in the post request

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

public class DatabaseSettings
{
	public string ConnectionString { get; set; } = null!;

	public string DatabaseName { get; set; } = null!;

	public string ProcessCollectionName { get; set; } = null!;
}