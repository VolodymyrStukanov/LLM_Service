using LLMSiteClassifier.Services.LLMService;
using LLMSiteClassifier.Services.LLMService.Extensions;
using LLMSiteClassifier.Sevices.MessageQueueService;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Error | LogEventLevel.Fatal)
    .WriteTo.File(
        "logs/all_logs.txt",
        rollingInterval: RollingInterval.Day,
        shared: true)
    .WriteTo.File(
        "logs/error_logs.txt",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Error | LogEventLevel.Fatal,
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

builder.Services.AddLlmHttpClients(builder.Configuration);
builder.Services.AddSingleton<LlmService>();
builder.Services.AddHostedService<MessageQueue>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
