namespace RagHub.Core.Settings;

public class RagSettings
{
    public StorageSettings Storage { get; set; } = new();
    public ChunkingSettings Chunking { get; set; } = new();
    public EmbeddingSettings Embedding { get; set; } = new();
    public GenerationSettings Generation { get; set; } = new();
    public RetrievalSettings Retrieval { get; set; } = new();
    public List<ApiKeyEntry> ApiKeys { get; set; } = [];
}

// Simple demo-grade external API key — enough to identify which team/caller is hitting
// the hub API. Not a substitute for real auth (out of scope for the POC).
public class ApiKeyEntry
{
    public string Key { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
}

public class StorageSettings
{
    public string UploadPath { get; set; } = "uploads";
}

public class ChunkingSettings
{
    public int MaxChunkSize { get; set; } = 1500;
    public int Overlap { get; set; } = 100;
}

public class EmbeddingSettings
{
    public string Provider { get; set; } = "ollama";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "bge-m3";
    public string OpenAiModel { get; set; } = "text-embedding-3-small";
}

public class GenerationSettings
{
    public string Provider { get; set; } = "ollama";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "gemma4:12b";
    public int NumCtx { get; set; } = 16384;
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public bool Think { get; set; } = false;
}

public class RetrievalSettings
{
    public int CandidateK { get; set; } = 20;
    public int FinalN { get; set; } = 5;
    public bool UseHybrid { get; set; } = true;
    public bool UseReranker { get; set; } = true;
    public bool UseMultiQuery { get; set; } = false;
    public int MultiQueryCount { get; set; } = 3;
    public string RerankerUrl { get; set; } = "http://localhost:8000";
}
