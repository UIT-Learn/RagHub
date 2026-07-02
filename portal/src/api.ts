export interface Document {
  id: number;
  name: string;
  category: string;
  type: string;
  status: 'Pending' | 'Processing' | 'Indexed' | 'Failed';
  chunkCount: number;
  embeddingModel: string | null;
  embeddingDim: number | null;
  chunkingProfileId: number | null;
  chunkSizeUsed: number | null;
  overlapUsed: number | null;
  errorMessage: string | null;
  uploadedAt: string;
}

export interface ChunkingProfile {
  id: number;
  name: string;
  strategy: string;
  maxChunkSize: number;
  overlap: number;
  createdAt: string;
}

export interface RetrievalConfig {
  candidateK: number;
  finalN: number;
  useHybrid: boolean;
  useReranker: boolean;
  useMultiQuery: boolean;
  multiQueryCount: number;
  updatedAt: string;
}

export interface ExpectedSource {
  documentId: number;
  documentName: string;
  headingPath: string | null;
  pageNo: number | null;
  snippet: string;
}

export interface EvaluationItem {
  id: number;
  question: string;
  expectedSources: ExpectedSource[];
  expectedAnswer: string | null;
  actualChunkIds: number[];
  actualAnswer: string | null;
  reciprocalRank: number | null;
  retrievalPassed: boolean | null;
  citationPassed: boolean | null;
  verifiedAt: string | null;
  createdAt: string;
  runAt: string | null;
}

export interface EvaluationSummary {
  totalQuestions: number;
  runCount: number;
  pendingReviewCount: number;
  recallAtK: number | null;
  mrr: number | null;
  citationAccuracy: number | null;
}

export interface Chunk {
  id: number;
  chunkIndex: number;
  content: string;
  headingPath: string | null;
  pageNo: number | null;
  charStart: number;
  charEnd: number;
  tokenCount: number;
  embeddingModel: string | null;
}

export interface SearchResultItem {
  chunkId: number;
  documentId: number;
  documentName: string;
  content: string;
  headingPath: string | null;
  pageNo: number | null;
  charStart: number;
  charEnd: number;
  score: number;
}

export interface SourceChunk {
  chunkId: number;
  documentId: number;
  documentName: string;
  headingPath: string | null;
  pageNo: number | null;
  charStart: number;
  charEnd: number;
}

export interface ChatHistoryTurn {
  role: 'user' | 'assistant';
  content: string;
}

export type ChatEvent =
  | { type: 'token'; data: { content: string } }
  | { type: 'sources'; data: { chunks: SourceChunk[] } };

// Demo-grade key for the external/hub-facing endpoints (search, chat, export) —
// must match an entry in RagSettings:ApiKeys on the backend.
const API_KEY = import.meta.env.VITE_API_KEY ?? 'dev-demo-key';
const apiKeyHeaders = { 'X-Api-Key': API_KEY };

export function createApiClient(baseUrl: string) {
  const base = baseUrl.replace(/\/$/, '');

  async function json<T>(path: string, init?: RequestInit): Promise<T> {
    const res = await fetch(`${base}${path}`, init);
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
    return res.json() as Promise<T>;
  }

  return {
    getDocuments: () => json<Document[]>('/documents'),
    getDocument: (id: number) => json<Document>(`/documents/${id}`),
    getChunks: (id: number) => json<Chunk[]>(`/documents/${id}/chunks`),
    reindex: (id: number, chunkingProfileId?: number) =>
      fetch(`${base}/documents/${id}/reindex`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ chunkingProfileId: chunkingProfileId ?? null }),
      }),
    deleteDocument: (id: number) =>
      fetch(`${base}/documents/${id}`, { method: 'DELETE' }),

    uploadDocument: (file: File, category: string, docType: string, chunkingProfileId?: number) => {
      const form = new FormData();
      form.append('file', file);
      form.append('category', category);
      if (docType) form.append('docType', docType);
      if (chunkingProfileId) form.append('chunkingProfileId', String(chunkingProfileId));
      return json<Document>('/documents', { method: 'POST', body: form });
    },

    getChunkingProfiles: () => json<ChunkingProfile[]>('/chunking-profiles'),
    createChunkingProfile: (name: string, strategy: string, maxChunkSize: number, overlap: number) =>
      json<ChunkingProfile>('/chunking-profiles', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, strategy, maxChunkSize, overlap }),
      }),
    updateChunkingProfile: (id: number, name: string, strategy: string, maxChunkSize: number, overlap: number) =>
      json<ChunkingProfile>(`/chunking-profiles/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, strategy, maxChunkSize, overlap }),
      }),
    deleteChunkingProfile: (id: number) =>
      fetch(`${base}/chunking-profiles/${id}`, { method: 'DELETE' }),

    getRetrievalConfig: () => json<RetrievalConfig>('/settings/retrieval'),
    updateRetrievalConfig: (cfg: Pick<RetrievalConfig, 'candidateK' | 'finalN' | 'useHybrid' | 'useReranker' | 'useMultiQuery' | 'multiQueryCount'>) =>
      json<RetrievalConfig>('/settings/retrieval', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(cfg),
      }),

    getEvaluations: () => json<EvaluationItem[]>('/evaluation'),
    createEvaluation: (question: string, expectedSources: ExpectedSource[], expectedAnswer: string) =>
      json<EvaluationItem>('/evaluation', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          question,
          expectedSources,
          expectedAnswer: expectedAnswer || null,
        }),
      }),
    deleteEvaluation: (id: number) =>
      fetch(`${base}/evaluation/${id}`, { method: 'DELETE' }),
    runEvaluation: (id: number) =>
      json<EvaluationItem>(`/evaluation/${id}/run`, { method: 'POST' }),
    runAllEvaluations: () =>
      json<EvaluationItem[]>('/evaluation/run-all', { method: 'POST' }),
    getEvaluationSummary: () => json<EvaluationSummary>('/evaluation/summary'),
    verifyEvaluation: (id: number) =>
      json<EvaluationItem>(`/evaluation/${id}/verify`, { method: 'POST' }),
    unverifyEvaluation: (id: number) =>
      json<EvaluationItem>(`/evaluation/${id}/verify`, { method: 'DELETE' }),

    search: (query: string, topK?: number) =>
      json<{ query: string; pipeline: string; results: SearchResultItem[] }>('/search', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...apiKeyHeaders },
        body: JSON.stringify({ query, topK }),
      }),

    exportDocument: async (id: number, fileName: string) => {
      const res = await fetch(`${base}/documents/${id}/export`, { headers: apiKeyHeaders });
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    },

    async *streamChat(
      query: string,
      history: ChatHistoryTurn[],
      signal?: AbortSignal,
    ): AsyncGenerator<ChatEvent> {
      const res = await fetch(`${base}/chat/query`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...apiKeyHeaders },
        body: JSON.stringify({ query, history }),
        signal,
      });
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);

      const reader = res.body!.getReader();
      const decoder = new TextDecoder();
      let buf = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buf += decoder.decode(value, { stream: true });
        const lines = buf.split('\n');
        buf = lines.pop() ?? '';
        for (const line of lines) {
          if (!line.startsWith('data: ')) continue;
          const raw = line.slice('data: '.length).trim();
          if (raw === '[DONE]') return;
          try {
            yield JSON.parse(raw) as ChatEvent;
          } catch { /* skip malformed */ }
        }
      }
    },

    submitFeedback: (
      query: string,
      answer: string,
      retrievedChunkIds: number[],
      rating: 'good' | 'bad',
    ) =>
      json('/chat/feedback', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...apiKeyHeaders },
        body: JSON.stringify({ query, answer, retrievedChunkIds, rating }),
      }),
  };
}

export const api = createApiClient('/api');
