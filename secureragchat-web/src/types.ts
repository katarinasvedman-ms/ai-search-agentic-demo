export type RetrievalPlane = 'Public' | 'Entitled';
export type RetrievalSource = 'AzureSearch' | 'Bing' | 'KnowledgeBase';
export type RetrievalMode = 'Traditional' | 'Agentic';

export interface ChatRequest {
  query: string;
  conversationId?: string;
  preferEntitledContent?: boolean;
  mode?: RetrievalMode;
}

export interface Citation {
  title: string;
  url?: string | null;
  sourceIndex: number;
}

export interface ChatDiagnostics {
  chunkCount: number;
  isAuthenticated: boolean;
  retrievalMode: RetrievalMode;
  retrievalSource: RetrievalSource;
}

export interface ChatResponse {
  answer: string;
  retrievalPlane: RetrievalPlane;
  citations: Citation[];
  diagnostics: ChatDiagnostics;
}

export interface ApiErrorPayload {
  error: string;
  correlationId?: string;
}

export interface ChatResult {
  data: ChatResponse;
  correlationId: string;
}

export interface TranscriptEntry {
  id: string;
  query: string;
  response?: ChatResponse;
  correlationId?: string;
  mode: 'anonymous' | 'authenticated';
  submittedAt: string;
  isLoading?: boolean;
  error?: string;
}