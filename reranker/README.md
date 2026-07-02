# Reranker Sidecar

FastAPI sidecar wrapping `bge-reranker-v2-m3` for cross-encoder reranking.
Called by `RagHub.API` over HTTP at `RagSettings:Retrieval:RerankerUrl`.

## Setup

```bash
cd reranker
python -m venv .venv
# Windows:
.venv\Scripts\activate
# macOS/Linux:
source .venv/bin/activate

pip install -r requirements.txt
```

First run downloads `BAAI/bge-reranker-v2-m3` from HuggingFace (~1.1 GB).

## Run

```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

## API

### POST /rerank

Request:
```json
{
  "query": "how many annual leave days",
  "candidates": [
    { "id": 1, "text": "Employees are entitled to 12 annual leave days per year." },
    { "id": 2, "text": "The office opens at 9am Monday to Friday." },
    { "id": 3, "text": "Annual leave accrues monthly at 1 day per month." }
  ]
}
```

Response (sorted by score descending):
```json
[
  { "id": 1, "score": 0.94 },
  { "id": 3, "score": 0.81 },
  { "id": 2, "score": 0.03 }
]
```

### GET /health

Returns `{"status": "ok", "model": "BAAI/bge-reranker-v2-m3"}`.

## Notes

- Set `UseReranker: false` in `RagSettings:Retrieval` to bypass this sidecar entirely.
- `use_fp16=True` halves memory usage with negligible quality loss (fine for POC).
- CPU inference is slow (~1–3s for 20 candidates) — acceptable for POC, not production.
