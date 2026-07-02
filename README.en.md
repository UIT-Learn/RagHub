*[Đọc bằng tiếng Việt](README.md)*

## PROJECT INFO

- **Course:** CS315.F21.CN2.TTNT — Advanced Machine Learning (Semester 2 - 2025/2026)
- **Instructor:** MSc. Đặng Việt Dũng
- **Student:** Nguyễn Minh Nhật — Student ID 25410104
- **University:** University of Information Technology — VNU-HCM
---

# RagHub — Internal Knowledge Assistant (RAG POC)

RagHub is a question-answering system built on **Retrieval-Augmented Generation (RAG)**, letting users ask natural-language questions over a company's internal document repository and get back answers with specific source citations. The entire system runs on self-hosted infrastructure (via Ollama) — no document content is sent to external services.

This is a course project built at proof-of-concept (POC) scale: 20–50 documents, around 10 test users, aimed at validating the feasibility of the approach before considering a larger-scale rollout.

---

## The problem

Companies typically hold many types of internal documents: HR policies, technical guides, API specs, and so on. When someone needs to look up a specific piece of information, they have to open each file, search manually (Ctrl+F), and hope they found the right — and most current — version.

RagHub solves this by letting users ask a question directly and get back an answer with a source citation, for example:

> **Question:** How many days of annual leave does an employee get per year?
> **Answer:** Employees are entitled to 12 days of annual leave per year.
> **Source:** LeavePolicy.pdf — Section 1.1 Annual Leave (page 2, chunk #2)

The answer is generated from actual text chunks retrieved from the uploaded documents, not from the model's own guesswork — every answer comes with a specific location in the source document that the user can verify against.

---

## Key features

- **Document upload & management**: supports PDF, DOCX, TXT, Markdown. Processing (chunking, embedding) runs in the background and doesn't block the UI during upload.
- **Conversational Q&A with context**: the system remembers earlier questions within the same session, so a follow-up like "what about section 2?" is still understood correctly.
- **Chunk-level source citations**: every answer is attached to a file name, section heading, page number, and character span — not just the file name.
- **Hybrid search**: combines semantic search (vector embeddings) with keyword search (full-text), fused using the Reciprocal Rank Fusion (RRF) algorithm for better coverage.
- **Cross-encoder reranking**: after the search step, a dedicated reranker model re-scores and re-orders chunks by their actual relevance to the question.
- **Live-tunable retrieval parameters**: settings such as how many chunks to retrieve or whether reranking is enabled can be changed from the Settings screen, with no server restart required.
- **Built-in quality evaluation toolkit**: measures Recall@k, MRR, and Citation Accuracy against a labeled golden question set.
- **Fully offline operation**: the embedding and generation models run locally through Ollama — data never leaves the server.

---

## Architecture overview

![RagHub system architecture overview](reports/FlowChart-en-Overview.png)

### Ingestion flow (runs once, at upload time)

![Document processing and indexing flow](reports/FlowChart-en-DocumentProccessing.png)

### Query flow (runs on every user question)

![RAG question-answering flow](reports/FlowChart-en-QA.png)

---

## Tech stack

| Component | Choice | Rationale |
|------|----------|-------|
| Backend | .NET 9, ASP.NET Core 9 | Full async support, explicit typing, built-in dependency injection |
| Frontend | React + Vite + Ant Design | Fast dev server, ready-made UI components suited for rapid POC work |
| Database | PostgreSQL 17 + pgvector | A single datastore for both metadata and vectors, no separate vector DB needed |
| ORM | EF Core + Npgsql + Pgvector.EF | The `<=>` (cosine distance) operator works directly in LINQ/SQL |
| Embedding | BGE-M3 via Ollama (1024 dimensions) | Runs on CPU, fully offline, no usage cost |
| Generation | Gemma 4 / Qwen via Ollama | Runs locally, no data sent externally |
| Reranker | bge-reranker-v2-m3 (Python sidecar) | Meaningfully improves ranking accuracy, still CPU-friendly |

**Can be swapped for cloud services via config** (no code changes needed):
- Embedding: OpenAI `text-embedding-3-small` (1536 dimensions) — requires an API key, sends data externally
- Generation: OpenAI `gpt-4o-mini` — requires an API key, sends data externally

---

## Project structure

```
RagHub/
├── src/
│   ├── RagHub.API/             Controllers, DI wiring, startup
│   ├── RagHub.Core/            Domain models, interfaces, DTOs (no infrastructure dependencies)
│   ├── RagHub.Infrastructure/  EF DbContext, Ollama/OpenAI providers, repositories
│   ├── RagHub.Worker/          Background indexing processing (IHostedService)
│   └── RagHub.AppHost/         .NET Aspire orchestration (optional)
├── portal/                     React + Vite frontend
├── reranker/                   Python FastAPI sidecar (cross-encoder)
├── db/
│   └── README.md               Schema setup / migration instructions
├── reports/                    Project report assets
```

**Layering rule:** `Core` has no dependency on EF or HTTP — it only defines interfaces. `Infrastructure` implements those interfaces. `API` wires them together via constructor injection. As a result, switching providers (e.g. from Ollama to OpenAI) is a configuration change only, never a source code change.

---

## Environment requirements

| Tool | Version | Role |
|---------|-----------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.x | Runs the backend |
| [Node.js](https://nodejs.org/) | 20+ | Runs the frontend |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | any | Runs PostgreSQL as a container |
| [Ollama](https://ollama.com/) | latest | Runs local AI models |
| [Python](https://www.python.org/downloads/) | 3.11+ | Runs the reranker sidecar |

---

## Setup & run guide

### Step 1 — Pull the AI models

```bash
# Embedding model — converts text into vectors
ollama pull bge-m3

# Generation model
ollama pull gemma4:e4b
```

The download is several GB and may take a few minutes depending on your connection. Ollama runs itself in the background once installed.

### Step 2 — Start the database

```bash
docker run -d --name raghub-postgres \
  -e POSTGRES_PASSWORD=raghub_dev \
  -e POSTGRES_DB=raghub \
  -p 5433:5432 \
  pgvector/pgvector:pg17
```

PostgreSQL 17 + pgvector runs on port **5433** to avoid conflicting with any existing Postgres install on the machine.

### Step 3 — Configure the backend

Create the file `src/RagHub.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=raghub;Username=postgres;Password=raghub_dev"
  },
  "RagSettings": {
    "Embedding": {
      "Provider": "ollama",
      "Model": "bge-m3",
      "Dimensions": 1024
    },
    "Generation": {
      "Provider": "ollama",
      "Model": "gemma4:e4b",
      "NumCtx": 16384
    },
    "Retrieval": {
      "CandidateK": 20,
      "FinalN": 5,
      "UseHybrid": true,
      "UseReranker": true,
      "RerankerUrl": "http://localhost:8000"
    }
  }
}
```

> **Note on `NumCtx`:** Ollama defaults to a 4K token context limit. If `NumCtx: 16384` isn't set, retrieved document chunks get silently truncated, degrading answer quality with no visible error.

### Step 4 — Create the database schema

```bash
dotnet ef database update \
  --project src/RagHub.Infrastructure \
  --startup-project src/RagHub.API
```

This creates 4 tables: `documents`, `chunks`, `feedback`, `evaluation`. The `chunks.embedding` column is of type `vector(1024)` — this dimension is fixed at table-creation time and must match the embedding model in use.

Verify the result:

```bash
docker exec raghub-postgres psql -U postgres -d raghub -c "\dt"
```

### Step 5 — Install the reranker sidecar

```bash
cd reranker
pip install -r requirements.txt
```

The `bge-reranker-v2-m3` model is downloaded automatically the first time the sidecar runs (next step).

> To skip reranking for now, set `"UseReranker": false` in the config — the system still runs normally, just with lower ranking accuracy.

### Step 6 — Start the whole system

There are 2 ways to do this — pick one:

**Option A — using the .NET Aspire AppHost (recommended, a single command)**

```bash
dotnet run --project src/RagHub.AppHost
```

`RagHub.AppHost` starts and supervises all 3 processes (reranker sidecar, backend API, frontend) in the correct dependency order: reranker → API → frontend. PostgreSQL and Ollama are still treated as external services, already running from steps 1–2. Aspire also opens a dashboard for monitoring logs/status at [http://localhost:15079](http://localhost:15079) (the login link with token is printed to the console on startup).

**Option B — running each process manually** (useful when debugging a service in isolation)

```bash
# Terminal 1 — reranker
cd reranker
uvicorn main:app --port 8000

# Terminal 2 — backend
dotnet run --project src/RagHub.API

# Terminal 3 — frontend
cd portal
npm install
npm run dev
```

Either way, you should end up with all 3 endpoints working:

```bash
curl http://localhost:8000/health
# {"status":"ok","model":"BAAI/bge-reranker-v2-m3"}

curl http://localhost:5079/api/health
# {"status":"healthy","database":"connected"}
```

Open the frontend at [http://localhost:5173](http://localhost:5173).

---

## Usage guide

### Uploading a document

1. Go to **Upload** in the left sidebar.
2. Select a PDF, DOCX, TXT, or Markdown file.
3. Choose the document type, or leave it on Auto Detect.
4. Click Upload.

The document appears in the **Documents** screen, moving through the states `Pending → Processing → Indexed`. If processing fails, the status becomes `Failed` with a specific error message; click **Reindex** to retry.

### Previewing chunks (Chunk Preview)

Click any document to open **Chunk Preview**, which shows every text chunk that was extracted, along with its heading path, page number, and character span. This screen is the first place to check when an answer looks wrong — it helps determine whether the issue is in chunking or in retrieval.

### Asking questions

Go to **Chat** and type a question. The system supports natural follow-up questions that reference prior context (e.g. "what about section 2?"). The processing pipeline: rewrite the question using conversation context, run hybrid search, rerank the results, then stream the generated answer along with a list of specific source citations.

### Evaluating quality

Go to **Evaluation** to run the golden Q&A set and measure:
- **Recall@k** — was the chunk containing the correct answer present in the top-k retrieval results?
- **MRR (Mean Reciprocal Rank)** — how highly was the correct chunk ranked?
- **Citation Accuracy** — did the final answer actually cite the chunk containing the correct information?

---

## Main API reference

| Endpoint | Method | Function |
|----------|--------|-----------|
| `/api/health` | GET | Check database connectivity |
| `/api/documents` | GET | List all documents |
| `/api/documents` | POST | Upload a document (multipart/form-data) |
| `/api/documents/{id}/chunks` | GET | List chunks for a document |
| `/api/documents/{id}/reindex` | POST | Re-index a document |
| `/api/search` | POST | Hybrid retrieve + rerank, used for debugging |
| `/api/chat/query` | POST | Full RAG query — body: `{ query, history? }`, returns an SSE stream |
| `/api/feedback` | POST | Submit helpful / not-helpful feedback for an answer |
| `/api/settings/retrieval` | GET/PUT | Read/write retrieval parameters, applied live without a restart |
| `/api/evaluation` | GET/POST | Manage the evaluation question set |
| `/api/evaluation/summary` | GET | Aggregated Recall@k, MRR, Citation Accuracy results |

---

## Configuration reference

All AI-related settings live under the `RagSettings` block in `appsettings.json`:

```json
{
  "RagSettings": {
    "Embedding": {
      "Provider": "ollama",        // "ollama" or "openai"
      "Model": "bge-m3",
      "Dimensions": 1024           // Fixed when the database is created — don't change casually
    },
    "Generation": {
      "Provider": "ollama",        // "ollama" or "openai"
      "Model": "gemma4:e4b",
      "NumCtx": 16384              // Required to override Ollama's silent 4K default
    },
    "Retrieval": {
      "CandidateK": 20,            // Number of chunks retrieved before reranking
      "FinalN": 5,                 // Number of chunks passed to the LLM
      "UseHybrid": true,           // false = dense search only (lower quality)
      "UseReranker": true,         // false = skip the reranker sidecar, no system error
      "RerankerUrl": "http://localhost:8000"
    }
  }
}
```

**Switching to OpenAI:**
1. Add the API key: `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."`
2. Change `Provider` to `"openai"`, and `Dimensions` to `1536`.
3. Drop the old database and re-run migrations (since the vector dimension has changed).
4. Re-upload all documents, since the old embeddings (BGE-M3) are incompatible with the new ones (OpenAI).

> **Security note:** using OpenAI means internal document content is sent to OpenAI's servers. For sensitive documents, keep the local Ollama configuration.

---

## Chunking strategy

The system automatically detects the document type and applies a matching chunking strategy:

| Document type | Strategy | Splitting method |
|------|------------|-----------|
| Policy | Heading-based | Splits at numbered headings (`1.1`, `2.3`, ...) |
| Technical | Section-based | Splits at sections like `Overview`, `Architecture`, `Deployment`, ... |
| API Spec | Endpoint-based | Splits at each HTTP verb (`POST /orders`, `GET /users`, ...) |

`MaxChunkSize` is an upper bound — if a section exceeds it, the system recursively splits it further. The `Overlap` parameter (overlap between chunks) only applies to these size-based fallback splits, not to structural splitting.

---

## Troubleshooting

**Document stuck at `Processing`**
The Worker runs in-process inside the API — check the API console logs for the error. Common causes: Ollama isn't running, or the configured model name is wrong.

**`vector(1024) dimension mismatch` error**
The embedding provider in config doesn't match the column's dimension in the database. If switching from BGE-M3 (1024 dims) to OpenAI (1536 dims), you must drop the `chunks` table and re-index all documents.

**Inaccurate answers, or the system says "not found" even though the document has the information**
1. Check the Chunk Preview screen — was the relevant content split into the correct chunk?
2. Call `POST /api/search` directly with the question to see which chunks are actually retrieved.
3. Confirm `NumCtx` is set correctly (default 16384) — if missing, Ollama silently truncates context.
4. Try toggling `UseHybrid` on/off to determine whether dense or sparse search is missing the chunk.

**Reranker sidecar crashes on startup**
Usually caused by a network failure while downloading the model. Check the log at `reranker/sidecar_err.log`, then re-run `uvicorn` — the download will resume. You can temporarily set `UseReranker: false` to run the system without it.

**Ollama not responding**
Run `ollama list` to check whether models have been downloaded. Run `ollama serve` if the daemon isn't running. By default, the API connects to Ollama at `http://localhost:11434`.

---

## Quality targets (POC)

| Metric | Target |
|--------|----------|
| Retrieval Recall@k | > 80% |
| Citation Accuracy | > 80% |
| p95 response time (end-to-end) | < 10 seconds |

These metrics are measured using the built-in evaluation toolkit (Evaluation screen), run against a labeled set of ~50 sample questions.
