# Edge/offline RAG demo notebook

Shows how to take a document export bundle from `GET /api/documents/{id}/export`
(see [DocumentDtos.cs](../src/RagHub.API/DTOs/DocumentDtos.cs)) and run retrieval +
generation against it **without the RagHub.API backend** — the scenario this export
exists for: a different device, holding only the JSON file and a local Ollama.

## Setup

```bash
cd notebooks
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
jupyter notebook
```

Requires a local [Ollama](https://ollama.com) with the same embedding model that
produced the export (default `bge-m3`), and pulled if not already:

```bash
ollama pull bge-m3
ollama pull gemma4:e2b   # or whichever generation model the edge device should run
```

## Usage

1. Download an export from the portal (Document Details → Export, requires the
   document to be `Indexed`) or via `GET /api/documents/{id}/export` with header
   `X-Api-Key: dev-demo-key`.
2. Drop the JSON file into `notebooks/samples/`.
3. Open `edge_rag_demo.ipynb`, set `BUNDLE_PATH` in the first code cell to that file,
   and run all cells.

## What it does

- Loads the bundle and checks `embeddingModel`/`embeddingDim` match what's configured
  here — refuses to proceed on a mismatch (vectors from a different model are not
  comparable, same invariant the backend enforces).
- Embeds a query via Ollama's `/api/embed`, brute-force cosine-ranks the bundle's
  chunks (no ANN index needed — the export is sized for a single document/POC corpus).
- Assembles top-k chunks into a prompt and streams an answer via Ollama's
  `/api/generate`, printing chunk citations (`heading_path`/`page_no`) alongside.

This intentionally does not call any RagHub.API endpoint — it proves the bundle is
self-sufficient for an edge/offline device.
