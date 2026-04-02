import type { ApiErrorPayload, ChatRequest, ChatResult } from '../types';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim() ?? '';

function buildUrl(path: string): string {
  if (!apiBaseUrl) {
    return path;
  }

  return `${apiBaseUrl.replace(/\/$/, '')}${path}`;
}

export async function sendChatRequest(
  request: ChatRequest,
  token?: string,
): Promise<ChatResult> {
  const correlationId = crypto.randomUUID();

  const response = await fetch(buildUrl('/api/chat'), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': correlationId,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(request),
  });

  const responseCorrelationId = response.headers.get('X-Correlation-Id') ?? correlationId;
  const payload = (await response.json()) as ChatResult['data'] | ApiErrorPayload;

  if (!response.ok) {
    const errorPayload = payload as ApiErrorPayload;
    throw new Error(
      `${errorPayload.error || 'Request failed'} (Correlation ID: ${errorPayload.correlationId ?? responseCorrelationId})`,
    );
  }

  return {
    data: payload as ChatResult['data'],
    correlationId: responseCorrelationId,
  };
}