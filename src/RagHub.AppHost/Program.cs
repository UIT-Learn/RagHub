var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL stays external — connection string comes from appsettings.Development.json.

// Ollama runs as a persistent external container — start once with:
// docker run -d --name raghub-ollama --gpus all -p 11434:11434 -v E:\Learn\ollamamodel:/root/.ollama/models --restart unless-stopped ollama/ollama:latest
var rerankerDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../reranker"));
var rerankerScript = Path.Combine(rerankerDir, "start.cmd");
var reranker = builder.AddExecutable("reranker", "cmd", rerankerDir, "/c", rerankerScript)
    .WithHttpEndpoint(port: 8000, name: "rerank", isProxied: false);

var api = builder.AddProject<Projects.RagHub_API>("api")
    .WaitFor(reranker);

builder.AddNpmApp("portal", "../../portal", scriptName: "dev")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WaitFor(api);

builder.Build().Run();
