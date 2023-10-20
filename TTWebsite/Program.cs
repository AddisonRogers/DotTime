var builder = WebApplication.CreateBuilder(args);


var app = builder.Build();



app.MapGet("/", () => "Hello World!");

app.Run();

//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-7.0