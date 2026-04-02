import type { ApiErrorPayload, ChatRequest, ChatResult } from '../types';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim() ?? '';

function buildUrl(path: string): string {
  if (!apiBaseUrl) {
    return path;
  }

  return `${apiBaseUrl.replace(/\/$/, '')}${path}`;
}

function normalizeChatResult(data: ChatResult['data']): ChatResult['data'] {
  const mode = data.retrievalDetails?.mode;
  if (!mode) {
    return data;
  }

  const normalizedMode = mode.toLowerCase() === 'agentic' ? 'agentic' : 'traditional';

  return {
    ...data,
    retrievalDetails: {
      ...data.retrievalDetails,
      mode: normalizedMode,
    },
  };
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
  const rawPayload = await response.text();

  let payload: ChatResult['data'] | ApiErrorPayload | null = null;
  if (rawPayload.trim().length > 0) {
    try {
      payload = JSON.parse(rawPayload) as ChatResult['data'] | ApiErrorPayload;
    } catch {
      throw new Error(
        `Received an invalid response from the API (Correlation ID: ${responseCorrelationId})`,
      );
    }
  }

  if (!response.ok) {
    const errorPayload = payload as ApiErrorPayload | null;
    const errorText = errorPayload?.error || `Request failed with status ${response.status}`;
    const errorCorrelationId = errorPayload?.correlationId ?? responseCorrelationId;

    throw new Error(
      `${errorText} (Correlation ID: ${errorCorrelationId})`,
    );
  }

  if (!payload) {
    throw new Error(`Received an empty response from the API (Correlation ID: ${responseCorrelationId})`);
  }

  return {
    data: normalizeChatResult(payload as ChatResult['data']),
    correlationId: responseCorrelationId,
  };
}