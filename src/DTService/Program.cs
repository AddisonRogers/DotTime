using DTService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<Worker>();

var host = builder.Build();
host.Run();