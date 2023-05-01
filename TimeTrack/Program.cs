namespace TimeTrack
{
    public static class Program
    {
        public static void Main()
        {



        }
        
        public static int wow(int a, int b)
        {
            return a+b;
        }
    }
}

/*
// The port number must match the port of the gRPC server.
using var channel = GrpcChannel.ForAddress("https://localhost:7042");
var client = new Greeter.GreeterClient(channel);
var reply = await client.SayHelloAsync(
    new HelloRequest { Name = "GreeterClient" });
Console.WriteLine("Greeting: " + reply.Message);
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
*/ // huhhhhhh gRPC



/*
Process[] localAll = Process.GetProcesses();
var processes = new Dictionary<string, processes>();
foreach (var process in localAll)
{
    process.EnableRaisingEvents = true;
    process.Exited += (o, eventArgs) => ProcessExited(o, eventArgs, process);
    if (!processes.TryAdd(process.ProcessName, new processes(default, true, process.StartTime)))
    { // Activates when there is something already in there with the process name
        if (!processes.TryGetValue(process.ProcessName, out var val)) throw new Exception("idk message Addison");
        processes[process.ProcessName] = new processes(val.Uptime, isRunning: true, process.StartTime); 
        // So if we have the process in database we update it to make it so it is running
    }; 
}

void ProcessExited(object? sender, EventArgs eventArgs, Process process)
{
    if (!processes.TryGetValue(process.ProcessName, out var val)) throw new Exception("idk message Addison");
    processes[process.ProcessName] = new processes(val.Uptime + (DateTime.Now - process.StartTime), isRunning: false, DateTime.MinValue); 
    // When the process is exited then we flip the running value and update the uptime 
}

public class processes
{
    public TimeSpan Uptime;
    public Boolean IsRunning;
    public DateTime? StartTime;
    public processes(TimeSpan uptime, bool isRunning, DateTime startTime)
    {
        Uptime = uptime;
        IsRunning = isRunning;
        StartTime = startTime;
    }
}

*/ // Process managmeent and stuff (Complete)

/*
IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null);

if (!isoStore.DirectoryExists("DatabaseTimeTrack"))
{
    isoStore.CreateDirectory("DatabaseTimeTrack");
    
}

var isoStream = new IsolatedStorageFileStream("TestStore.txt", FileMode., isoStore);
    
using (StreamReader reader = new StreamReader(isoStream)) Console.WriteLine(reader.ReadToEnd());

var isoStream = new IsolatedStorageFileStream("TestStore.txt", FileMode.CreateNew, isoStore);
using StreamWriter writer = new StreamWriter(isoStream);
foreach (var variProcess in localAll) writer.WriteLine(variProcess.ProcessName);

*/ // Isolated storage (Complete)

/*
public class BloggingContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    public string DbPath { get; }

    public BloggingContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = System.IO.Path.Join(path, "blogging.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}

*/ // SQLite + EFcore
/*
 * using System;
using System.Linq;

using var db = new BloggingContext();

// Note: This sample requires the database to be created before running.
Console.WriteLine($"Database path: {db.DbPath}.");

// Create
Console.WriteLine("Inserting a new blog");
db.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
db.SaveChanges();

// Read
Console.WriteLine("Querying for a blog");
var blog = db.Blogs
    .OrderBy(b => b.BlogId)
    .First();

// Update
Console.WriteLine("Updating the blog and adding a post");
blog.Url = "https://devblogs.microsoft.com/dotnet";
blog.Posts.Add(
    new Post { Title = "Hello World", Content = "I wrote an app using EF Core!" });
db.SaveChanges();

// Delete
Console.WriteLine("Delete the blog");
db.Remove(blog);
db.SaveChanges();
 */


