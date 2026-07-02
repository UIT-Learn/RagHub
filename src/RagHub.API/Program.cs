using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using RagHub.Core.Interfaces;
using RagHub.Core.Settings;
using RagHub.Infrastructure.Chunking;
using RagHub.Infrastructure.Parsing;
using RagHub.Infrastructure.Persistence;
using RagHub.Infrastructure.Chat;
using RagHub.Infrastructure.Providers;
using RagHub.Infrastructure.Retrieval;
using RagHub.Worker;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Config
builder.Services.Configure<RagSettings>(builder.Configuration.GetSection("RagSettings"));

// Database
builder.Services.AddDbContext<RagDbContext>(opt =>
{
    var conn = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
    var dsBuilder = new NpgsqlDataSourceBuilder(conn);
    dsBuilder.EnableDynamicJson();
    dsBuilder.UseVector();
    var dataSource = dsBuilder.Build();
    opt.UseNpgsql(dataSource, npgsql => npgsql.UseVector())
       .UseSnakeCaseNamingConvention();
});

// Parsers (registered as IDocumentParser; all implementations are resolved via GetServices<IDocumentParser>())
builder.Services.AddTransient<IDocumentParser, PdfParser>();
builder.Services.AddTransient<IDocumentParser, DocxParser>();
builder.Services.AddTransient<IDocumentParser, PlainTextParser>();

// Chunker
builder.Services.AddTransient<IChunker, StructuralChunker>();

// Embedding provider — chosen by RagSettings:Embedding:Provider
var embeddingProvider = builder.Configuration["RagSettings:Embedding:Provider"] ?? "ollama";
if (embeddingProvider.Equals("openai", StringComparison.OrdinalIgnoreCase))
{
    var apiKey = builder.Configuration["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("OpenAI:ApiKey is required when Provider=openai.");
    builder.Services.AddHttpClient<IEmbeddingProvider, OpenAiEmbeddingProvider>(c =>
    {
        c.BaseAddress = new Uri("https://api.openai.com/");
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    });
}
else
{
    var ollamaBase = builder.Configuration["RagSettings:Embedding:OllamaBaseUrl"] ?? "http://localhost:11434";
    builder.Services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>(c =>
    {
        c.BaseAddress = new Uri(ollamaBase.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromMinutes(5); // BGE-M3 batch can be slow on CPU
    });
}

// Reranker sidecar
var rerankerUrl = builder.Configuration["RagSettings:Retrieval:RerankerUrl"] ?? "http://localhost:8000";
builder.Services.AddHttpClient<IReranker, HttpReranker>(c =>
{
    c.BaseAddress = new Uri(rerankerUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(30);
});

// Retrieval service
builder.Services.AddScoped<IRetrievalService, RetrievalService>();

// Generation provider — chosen by RagSettings:Generation:Provider
var generationProvider = builder.Configuration["RagSettings:Generation:Provider"] ?? "ollama";
if (generationProvider.Equals("openai", StringComparison.OrdinalIgnoreCase))
{
    var apiKey = builder.Configuration["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("OpenAI:ApiKey is required when Provider=openai.");
    builder.Services.AddHttpClient<IGenerationProvider, OpenAiGenerationProvider>(c =>
    {
        c.BaseAddress = new Uri("https://api.openai.com/");
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        c.Timeout = TimeSpan.FromMinutes(10);
    });
}
else
{
    var ollamaBase = builder.Configuration["RagSettings:Generation:OllamaBaseUrl"] ?? "http://localhost:11434";
    builder.Services.AddHttpClient<IGenerationProvider, OllamaGenerationProvider>(c =>
    {
        c.BaseAddress = new Uri(ollamaBase.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromMinutes(10);
    });
}

// Chat service
builder.Services.AddScoped<ChatService>();

// Background worker — Ignore so a worker crash doesn't take down the API host
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
builder.Services.AddHostedService<IndexingWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.Run();
