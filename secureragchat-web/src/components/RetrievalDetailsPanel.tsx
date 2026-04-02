import { useState } from 'react';
import type { ChatResponse, RetrievalDetails } from '../types';
import type { RetrievalMode } from '../types';

type RetrievalDetailsPanelProps = {
  response?: ChatResponse;
  requestedMode: RetrievalMode;
  requesterMode: 'anonymous' | 'authenticated';
  errorMessage?: string;
};

type NormalizedMode = 'traditional' | 'agentic';

function resolveMode(response: ChatResponse | undefined, requestedMode: RetrievalMode): NormalizedMode {
  const detailsMode = response?.retrievalDetails?.mode;
  if (detailsMode) {
    return detailsMode;
  }

  if (!response) {
    return requestedMode === 'Agentic' ? 'agentic' : 'traditional';
  }

  return response.diagnostics.retrievalMode === 'Agentic' ? 'agentic' : 'traditional';
}

function formatAuthorization(
  response: ChatResponse | undefined,
  requesterMode: 'anonymous' | 'authenticated',
  details?: RetrievalDetails,
): string {
  if (details?.authorization) {
    return details.authorization;
  }

  if (!response) {
    return requesterMode === 'authenticated' ? 'user' : 'guest';
  }

  if (response.retrievalPlane === 'Entitled') {
    return 'entitled user';
  }

  return response.diagnostics.isAuthenticated ? 'user' : 'guest';
}

function detailsRows(
  response: ChatResponse | undefined,
  requestedMode: RetrievalMode,
  requesterMode: 'anonymous' | 'authenticated',
  errorMessage?: string,
): Array<{ label: string; value: string }> {
  const details = response?.retrievalDetails;
  const mode = resolveMode(response, requestedMode);
  const rows: Array<{ label: string; value: string }> = [];

  rows.push({ label: 'Mode', value: mode === 'traditional' ? 'Traditional' : 'Agentic' });

  if (mode === 'traditional') {
    rows.push({
      label: 'Retrieval style',
      value: details?.retrievalStyle ?? 'Hybrid + semantic',
    });
    rows.push({
      label: 'Query',
      value: details?.query ?? 'not available',
    });
    rows.push({
      label: 'Filters',
      value: details?.filters ?? 'security trimming applied by retrieval plane',
    });
  } else {
    rows.push({
      label: 'Retrieval style',
      value: details?.retrievalStyle ?? 'Knowledge base',
    });
    rows.push({
      label: 'Query construction',
      value: details?.queryConstruction ?? 'handled by system',
    });
    rows.push({
      label: 'Knowledge base',
      value: details?.knowledgeBaseUsed === false ? 'not used' : 'used',
    });
  }

  rows.push({
    label: 'Results',
    value: details?.resultsCount?.toString() ?? response?.diagnostics.chunkCount?.toString() ?? 'not available',
  });

  rows.push({
    label: 'Authorization',
    value: formatAuthorization(response, requesterMode, details),
  });

  if (errorMessage) {
    rows.push({
      label: 'Request status',
      value: 'failed before response details were available',
    });
  }

  return rows;
}

export function RetrievalDetailsPanel({ response, requestedMode, requesterMode, errorMessage }: RetrievalDetailsPanelProps) {
  const [mobileExpanded, setMobileExpanded] = useState(true);
  const mode = resolveMode(response, requestedMode);
  const badgeText = mode === 'traditional' ? 'Manual retrieval' : 'System retrieval';
  const rows = detailsRows(response, requestedMode, requesterMode, errorMessage);

  return (
    <aside className="retrieval-panel" aria-label="Retrieval details">
      <div className="retrieval-panel-header">
        <h3>Retrieval details</h3>
        <span className={`retrieval-badge ${mode}`}>{badgeText}</span>
      </div>

      <button
        type="button"
        className="retrieval-mobile-toggle"
        aria-expanded={mobileExpanded}
        onClick={() => setMobileExpanded((current) => !current)}
      >
        {mobileExpanded ? 'Hide retrieval details' : 'Show retrieval details'}
      </button>

      <div className={`retrieval-panel-content ${mobileExpanded ? '' : 'collapsed'}`}>
        {rows.map((row) => (
          <div key={row.label} className="retrieval-row">
            <span className="retrieval-row-label">{row.label}</span>
            <strong>{row.value || 'not available'}</strong>
          </div>
        ))}
      </div>
    </aside>
  );
}
