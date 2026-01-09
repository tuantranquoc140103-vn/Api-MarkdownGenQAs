using System.ClientModel;
using Amazon.S3;
using DotNetEnv;
using StackExchange.Redis;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Markdig;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Repositories;
using MarkdownGenQAs.Interfaces;
using MarkdownGenQAs.Services;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Extensions.Http;
using Scalar.AspNetCore;
using Serilog;
using MarkdownGenQAs.Options;
using MarkdownGenQAs.Models;

Env.Load();
var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Application starting up...");

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError() // Tự động bắt lỗi 5xx hoặc 408 (Timeout)
    .OrResult(msg => msg.StatusCode != System.Net.HttpStatusCode.OK) // Hoặc bất kỳ lỗi nào khác 200
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // Exponential backoff (2s, 4s, 8s)

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Configuration.AddEnvironmentVariables();

Log.Information("Loading configuration from appsettings.json and environment variables...");

ChunkOption chunkOption = builder.Configuration.GetRequiredSection(ChunkOption.NameSection).Get<ChunkOption>() ?? throw new ArgumentNullException("ChunkOption is missing in appsettings.json");
LlmProviderOptions llmProviderOptions = builder.Configuration.GetRequiredSection(LlmProviderOptions.NameSection).Get<LlmProviderOptions>() ?? throw new ArgumentNullException("LlmProviderOptions is missing in appsettings.json");
SystemPrompts systemPrompt = builder.Configuration.GetRequiredSection(SystemPrompts.SectionName).Get<SystemPrompts>() ?? throw new ArgumentNullException("SystemPrompt is missing in appsettings.json");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string"
        + "'DefaultConnection' not found.");
// Hangfire Configuration
var hangfireOptions = builder.Configuration
    .GetRequiredSection(HangfireOptions.SectionName)
    .Get<HangfireOptions>()
    ?? throw new ArgumentNullException("Hangfire configuration is missing");


Log.Information("Configuration loaded successfully");

// Service
Log.Information("Registering services...");

builder.Services.AddKeyedSingleton<ChatClient>(LlmProvider.Vllm, (sp, key) =>
    {
        Log.Information("Initializing Vllm ChatClient with BaseUrl: {BaseUrl}", llmProviderOptions.Vllm.BaseUrl);
        var options = new OpenAIClientOptions { Endpoint = new Uri(llmProviderOptions.Vllm.BaseUrl) };
        var client = new OpenAIClient(new ApiKeyCredential(llmProviderOptions.Vllm.ApiKey ?? "no-api-key"), options);
        var models = llmProviderOptions.Vllm.Models;
        if (models.Count == 0)
        {
            Log.Warning("Vllm Models list is empty, using default 'no-model'");
            // bắt buộc phải set vì chat client khong cho phép null cho thuộc tính model name
            return client.GetChatClient("no-model");
        }

        Log.Information("Vllm ChatClient initialized with model: {ModelName}", llmProviderOptions.Vllm.Models[0]?.ModelName);
        return client.GetChatClient(llmProviderOptions.Vllm.Models[0]?.ModelName);
    });
builder.Services.AddKeyedSingleton<ChatClient>(LlmProvider.Nvidia, (sp, key) =>
{
    Log.Information("Initializing Nvidia ChatClient with BaseUrl: {BaseUrl}", llmProviderOptions.Nvidia.BaseUrl);
    string? apiKey = llmProviderOptions.Nvidia.ApiKey;
    if (string.IsNullOrEmpty(apiKey))
    {
        Log.Error("Nvidia ApiKey is missing in appsettings.json or .env file");
        throw new ArgumentNullException(apiKey, "LlmProvider:Nvidia ApiKey is missing in appsettings.json or .env file");
    }
    var models = llmProviderOptions.Nvidia.Models;
    if (models.Count == 0)
    {
        Log.Error("Nvidia Models list is empty in appsettings.json");
        throw new ArgumentException("LlmProvider:Nvidia Models list is empty in appsettings.json");
    }
    var llmOption = models[0];
    Log.Information("Nvidia ChatClient initialized with model: {ModelName}", llmOption.ModelName);
    return new ChatClient(
        model: llmOption.ModelName,
        credential: new ApiKeyCredential(apiKey),
        options: new OpenAIClientOptions { Endpoint = new Uri(llmProviderOptions.Nvidia.BaseUrl) }
    );
});
builder.Services.AddHttpClient<TokenCountService>(
    client =>
    {
        string url = builder.Configuration.GetRequiredSection("TokenCountService:BaseUrl").Value ?? throw new ArgumentNullException("TokenCountService:BaseUrl Key is missing in env file or appsettings.json");
        client.BaseAddress = new Uri(url);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    }
).AddPolicyHandler(retryPolicy);
builder.Services.AddScoped<IGenQAsService, GenQAsService>();
builder.Services.AddKeyedSingleton<LlmChatCompletionBase, NvidiaService>(LlmProvider.Nvidia);
builder.Services.AddKeyedSingleton<LlmChatCompletionBase, VllmService>(LlmProvider.Vllm);
builder.Services.AddScoped<IMarkdownService, MarkdownService>();
builder.Services.AddSingleton<IJsonService, JsonService>();
builder.Services.AddSingleton<IProcessBroadcaster, ProcessBroadcaster>();

// Repositories
// builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
builder.Services.AddScoped<ICategoryFileRepository, CategoryFileRepository>();
builder.Services.AddScoped<ILogMessageRepository, LogMessageRepository>();

// Option
builder.Services.AddSingleton<MarkdownPipeline>(sp =>
{
    return new MarkdownPipelineBuilder()
                                 .UseAdvancedExtensions()
                                 .Build();
});

builder.Services.Configure<ChunkOption>(builder.Configuration.GetRequiredSection(ChunkOption.NameSection));
builder.Services.Configure<LlmProviderOptions>(builder.Configuration.GetRequiredSection(LlmProviderOptions.NameSection));
builder.Services.Configure<SystemPrompts>(builder.Configuration.GetRequiredSection(SystemPrompts.SectionName));
builder.Services.Configure<HangfireOptions>(builder.Configuration.GetRequiredSection(HangfireOptions.SectionName));

// AWS Service
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<IS3Service, S3Service>();




// Redis Connection for Pub/Sub and Cache
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(hangfireOptions.RedisConnection));

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = hangfireOptions.RedisConnection;
    options.InstanceName = "MarkdownGenQAs:";
});

builder.Services.AddSingleton<ICacheService, RedisCacheService>();

Log.Information("Configuring Hangfire with Redis storage: {RedisConnection}", hangfireOptions.RedisConnection);

builder.Services.AddHangfire(config =>
{
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseRedisStorage(hangfireOptions.RedisConnection, new RedisStorageOptions
        {
            Prefix = "hangfire:markdowngenqas:",
            ExpiryCheckInterval = TimeSpan.FromHours(1)
        });
});

var workerCount = hangfireOptions.WorkerCount > 0
    ? hangfireOptions.WorkerCount
    : Environment.ProcessorCount * 2;

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = workerCount;
    options.ServerName = $"{Environment.MachineName}:MarkdownGenQAs";
});

Log.Information("Hangfire server configured with {WorkerCount} workers", workerCount);

// Background Jobs
builder.Services.AddScoped<IBackgroundJobService, BackgroundJobService>();

// Factory
builder.Services.AddSingleton<ILlmClientFactory, LlmClientFactory>();
builder.Services.AddSingleton<ILlmServiceFactory, LlmServiceFactory>();

builder.Services.AddDbContext<ApplicationContext>(option =>
{
    option.UseNpgsql(connectionString);
});

// Add services to the container.
builder.Services.AddCors(options =>
{
    // Thỏa mãn yêu cầu: Allow *
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });

    /*
    // Option: Cấu hình allow các domain cụ thể từ appsettings.json hoặc environment variables
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
    if (allowedOrigins != null && allowedOrigins.Length > 0)
    {
        options.AddPolicy("Restricted",
            builder =>
            {
                builder.WithOrigins(allowedOrigins)
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
    }
    */
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Cấu hình thời gian chờ shutdown ggraceful
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

Log.Information("All services registered successfully");



var app = builder.Build();

Log.Information("Application built successfully");

// Initialize S3 Buckets
using (var scope = app.Services.CreateScope())
{
    var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
    await s3Service.InitializeBucketsAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Log.Information("Running in Development environment");
    app.MapOpenApi();
    app.MapScalarApiReference("/docs");
}
else
{
    Log.Information("Running in {Environment} environment", app.Environment.EnvironmentName);
}

app.UseSerilogRequestLogging();
app.UseCors("AllowAll"); // Sử dụng policy AllowAll
// app.UseCors("Restricted"); // Hoặc switch sang policy Restricted nếu cần
app.UseHttpsRedirection();

app.MapControllers();

// Hangfire Dashboard
app.UseHangfireDashboard(hangfireOptions.DashboardPath, new DashboardOptions
{
    DashboardTitle = hangfireOptions.DashboardTitle,
    AppPath = "/",
    StatsPollingInterval = 5000
});

Log.Information("Hangfire Dashboard available at: {DashboardPath}", hangfireOptions.DashboardPath);

// Configure recurring jobs
JobScheduler.ConfigureRecurringJobs();
Log.Information("Recurring jobs configured");

Log.Information("Starting web application...");
Log.Information("Application is running. Press Ctrl+C to shut down.");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Application shutting down...");
    Log.CloseAndFlush();
}
