import numpy as np
from fastapi import FastAPI
from pydantic import BaseModel
from sentence_transformers import CrossEncoder

app = FastAPI()
model = CrossEncoder("BAAI/bge-reranker-v2-m3", max_length=512)


class Candidate(BaseModel):
    id: int
    text: str


class RerankRequest(BaseModel):
    query: str
    candidates: list[Candidate]


class RerankResult(BaseModel):
    id: int
    score: float


@app.post("/rerank", response_model=list[RerankResult])
def rerank(request: RerankRequest) -> list[RerankResult]:
    if not request.candidates:
        return []
    pairs = [[request.query, c.text] for c in request.candidates]
    raw_scores = model.predict(pairs)
    # sigmoid-normalize to [0, 1]
    scores = (1 / (1 + np.exp(-raw_scores))).tolist()
    results = [
        RerankResult(id=c.id, score=float(s))
        for c, s in zip(request.candidates, scores)
    ]
    return sorted(results, key=lambda r: r.score, reverse=True)


@app.get("/health")
def health():
    return {"status": "ok", "model": "BAAI/bge-reranker-v2-m3"}
