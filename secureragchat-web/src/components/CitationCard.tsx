import type { Citation, RetrievalMode, RetrievalPlane } from '../types';

type CitationCardProps = {
  citation: Citation;
  href?: string;
  mode: 'anonymous' | 'authenticated';
  retrievalMode: RetrievalMode;
  retrievalPlane: RetrievalPlane;
};

function getSourceLabel(href: string | undefined): string {
  if (!href) {
    return 'Source unavailable';
  }

  try {
    return new URL(href).hostname.replace(/^www\./i, '');
  } catch {
    return 'Open source';
  }
}

export function CitationCard({ citation, href, mode, retrievalMode, retrievalPlane }: CitationCardProps) {
  return (
    <article className="result-card">
      <div className="result-card-meta">
        <span className="result-index">Source {citation.sourceIndex}</span>
        <span className="result-badge">{retrievalPlane === 'Entitled' ? 'User scoped' : 'Public'}</span>
        <span className="result-badge">{retrievalMode}</span>
        <span className="result-badge subtle">{mode === 'authenticated' ? 'Authenticated request' : 'Guest request'}</span>
      </div>

      <h3>{citation.title}</h3>
      <p>
        Grounding evidence used for the latest answer. Open the underlying document to verify what the assistant cited.
      </p>

      <div className="result-card-footer">
        <span className="source-host">{getSourceLabel(href)}</span>
        {href ? (
          <a href={href} rel="noreferrer" target="_blank">
            Open document
          </a>
        ) : (
          <span className="source-unavailable">Link not available</span>
        )}
      </div>
    </article>
  );
}
