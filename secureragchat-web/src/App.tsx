import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { sendChatRequest } from './api/chatClient';
import {
  getAccessToken,
  getAuthConfigurationState,
  getSignedInAccount,
  signInWithMicrosoft,
  signOutMicrosoft,
} from './auth/msalClient';
import { RetrievalDetailsPanel } from './components/RetrievalDetailsPanel';
import type { Citation, RetrievalMode, TranscriptEntry } from './types';
import ericssonMock from '../ericsson-search.png';
import './App.css';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim() ?? '';

function resolveCitationUrl(url: string): string {
  if (!url.startsWith('/')) {
    return url;
  }

  if (!apiBaseUrl) {
    return url;
  }

  return `${apiBaseUrl.replace(/\/$/, '')}${url}`;
}

function withDemoAuthHint(url: string, mode: 'anonymous' | 'authenticated'): string {
  if (mode !== 'authenticated') {
    return url;
  }

  const separator = url.includes('?') ? '&' : '?';
  return `${url}${separator}demoAuth=1`;
}

function isWeakKnowledgeBaseCitation(citation: { title: string; url?: string | null }): boolean {
  return !citation.url && citation.title.trim().toLowerCase() === 'knowledge base result';
}

function getDisplayCitations(citations: Citation[]): Citation[] {
  return citations.filter((citation) => !isWeakKnowledgeBaseCitation(citation));
}

function isNoAnswerResponse(answer: string): boolean {
  const normalizedAnswer = answer.trim().toLowerCase();

  return normalizedAnswer === "i don't know based on the available information."
    || normalizedAnswer === "i don't have relevant information to answer your question.";
}

function shouldHideCitationBlock(response: {
  answer: string;
  citations: Citation[];
  diagnostics: { chunkCount: number };
}) {
  return getDisplayCitations(response.citations).length === 0
    && (response.diagnostics.chunkCount === 0 || isNoAnswerResponse(response.answer));
}

function getEmptyCitationMessage(response: {
  answer: string;
  citations: Citation[];
  diagnostics: { chunkCount: number };
}) {
  if (response.diagnostics.chunkCount === 0) {
    return 'No relevant sources were retrieved for this question.';
  }

  if (isNoAnswerResponse(response.answer)) {
    return 'No relevant sources were retrieved for this question.';
  }

  return 'No citations were returned for this answer.';
}

function formatTimestamp(timestamp: string): string {
  return new Date(timestamp).toLocaleString([], {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

function buildCitationHref(citation: Citation, mode: 'anonymous' | 'authenticated'): string | undefined {
  if (!citation.url) {
    return undefined;
  }

  return withDemoAuthHint(resolveCitationUrl(citation.url), mode);
}

function App() {
  const [mode, setMode] = useState<'anonymous' | 'authenticated'>('anonymous');
  const [retrievalMode, setRetrievalMode] = useState<RetrievalMode>('Traditional');
  const [isChatExpanded, setIsChatExpanded] = useState(false);
  const [query, setQuery] = useState('');
  const [conversationId] = useState(() => crypto.randomUUID());
  const [entries, setEntries] = useState<TranscriptEntry[]>([]);
  const [isSignedIn, setIsSignedIn] = useState(false);
  const [authBusy, setAuthBusy] = useState(false);

  const authConfiguration = useMemo(() => getAuthConfigurationState(), []);

  useEffect(() => {
    async function hydrateAccount() {
      if (!authConfiguration.isConfigured) {
        return;
      }

      const account = await getSignedInAccount();
      if (!account) {
        return;
      }

      setIsSignedIn(true);
      setMode('authenticated');
    }

    hydrateAccount().catch((error) => {
      console.error(error);
    });
  }, [authConfiguration.isConfigured]);

  async function handleSignIn() {
    try {
      setAuthBusy(true);

      if (authConfiguration.isConfigured) {
        await signInWithMicrosoft();
        setIsSignedIn(true);
      }

      setMode('authenticated');
    } catch (error) {
      alert(error instanceof Error ? error.message : 'Sign-in failed.');
    } finally {
      setAuthBusy(false);
    }
  }

  async function handleSignOut() {
    try {
      setAuthBusy(true);

      if (authConfiguration.isConfigured && isSignedIn) {
        await signOutMicrosoft();
      }

      setIsSignedIn(false);
      setMode('anonymous');
    } catch (error) {
      alert(error instanceof Error ? error.message : 'Sign-out failed.');
    } finally {
      setAuthBusy(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedQuery = query.trim();
    if (!trimmedQuery) {
      return;
    }

    const entryId = crypto.randomUUID();
    const runtimeMode = mode;
    const runtimeRetrievalMode = retrievalMode;

    setEntries((current) => [
      {
        id: entryId,
        query: trimmedQuery,
        mode: runtimeMode,
        retrievalMode: runtimeRetrievalMode,
        submittedAt: new Date().toISOString(),
        isLoading: true,
      },
      ...current,
    ]);
    setQuery('');

    try {
      const token =
        runtimeMode === 'authenticated' && authConfiguration.isConfigured
          ? await getAccessToken()
          : undefined;

      const result = await sendChatRequest(
        {
          query: trimmedQuery,
          conversationId,
          preferEntitledContent: runtimeMode === 'authenticated' ? true : undefined,
          mode: runtimeRetrievalMode,
        },
        token,
      );

      setEntries((current) =>
        current.map((entry) =>
          entry.id === entryId
            ? {
                ...entry,
                isLoading: false,
                response: result.data,
                correlationId: result.correlationId,
              }
            : entry,
        ),
      );
    } catch (error) {
      setEntries((current) =>
        current.map((entry) =>
          entry.id === entryId
            ? {
                ...entry,
                isLoading: false,
                error: error instanceof Error ? error.message : 'Unknown error',
              }
            : entry,
        ),
      );
    }
  }

  const latestEntry = entries[0];
  const latestResponse = latestEntry?.response;
  const latestCitations = latestResponse ? getDisplayCitations(latestResponse.citations) : [];
  const recentQueries = entries.slice(1, 4);

  return (
    <div className="search-page">
      <main className="page-content">
        <section className="mock-panel" aria-label="Ericsson mock search panel">
          <div className="mock-canvas">
            <img className="mock-image" src={ericssonMock} alt="Ericsson-style search mock" />

            <aside className={`embedded-chat ${isChatExpanded ? 'expanded' : ''}`} aria-label="Embedded chat panel">
              <section className="assistant-card-shell compact-pane">
                <button
                  className="chat-expand-button"
                  type="button"
                  aria-label={isChatExpanded ? 'Minimize chat panel' : 'Expand chat panel'}
                  onClick={() => setIsChatExpanded((current) => !current)}
                >
                  {isChatExpanded ? '↙ Minimize' : '↗ Expand'}
                </button>

                <div className="assistant-card-header">
                  <div>
                    <h2>How can I help you today?</h2>
                  </div>
                </div>

                <div className="header-actions pane-actions">
                  <span className="status-chip">{mode === 'anonymous' ? 'Public-only scope' : 'User ACL scope'}</span>
                  <div className="auth-actions">
                    <span className={`identity-pill ${mode}`}>
                      {mode === 'anonymous' ? 'Guest' : 'Authenticated'}
                    </span>
                    <button
                      className="identity-button"
                      type="button"
                      disabled={authBusy}
                      onClick={mode === 'anonymous' ? handleSignIn : handleSignOut}
                    >
                      {authBusy ? 'Working...' : mode === 'anonymous' ? 'Log in' : 'Log out'}
                    </button>
                  </div>
                </div>

                <form className="search-form embedded-form" onSubmit={handleSubmit}>
                  <div className="toggle-group" role="group" aria-label="Retrieval mode">
                    <button
                      className={`toggle-button ${retrievalMode === 'Traditional' ? 'active' : ''}`}
                      type="button"
                      onClick={() => setRetrievalMode('Traditional')}
                    >
                      Traditional
                    </button>
                    <button
                      className={`toggle-button ${retrievalMode === 'Agentic' ? 'active' : ''}`}
                      type="button"
                      onClick={() => setRetrievalMode('Agentic')}
                    >
                      Agentic
                    </button>
                  </div>
                  <div className="search-input-row">
                    <input
                      className="search-input"
                      maxLength={4000}
                      placeholder="Ask the assistant"
                      type="text"
                      value={query}
                      onChange={(event) => setQuery(event.target.value)}
                    />
                    <button className="search-button" type="submit">
                      Ask
                    </button>
                  </div>
                </form>

                {recentQueries.length > 0 ? (
                  <div className="recent-query-row" aria-label="Recent queries">
                    <span>Quick reuse</span>
                    {recentQueries.map((entry) => (
                      <button
                        key={`recent-${entry.id}`}
                        className="recent-query-chip"
                        type="button"
                        onClick={() => setQuery(entry.query)}
                      >
                        {entry.query}
                      </button>
                    ))}
                  </div>
                ) : null}

                {!latestEntry ? (
                  <div className="assistant-empty-state">
                    <p>Ask a question to show the answer, citations, and retrieval details in this embedded panel.</p>
                  </div>
                ) : latestEntry.isLoading ? (
                  <section className="loading-card">
                    <span className="loading-dot" />
                    Retrieving authorized context.
                  </section>
                ) : latestEntry.error ? (
                  <section className="error-card compact">
                    <h2>Request failed</h2>
                    <p>{latestEntry.error}</p>
                  </section>
                ) : latestResponse ? (
                  <div className="assistant-history single-answer">
                    <article className="assistant-history-item">
                      <div className="assistant-history-meta">
                        <span>{latestEntry.retrievalMode}</span>
                        <span>{latestEntry.mode === 'anonymous' ? 'Guest' : 'Azure user'}</span>
                        <span>{formatTimestamp(latestEntry.submittedAt)}</span>
                      </div>
                      <h3>{latestEntry.query}</h3>
                      <p>{latestResponse.answer}</p>
                    </article>

                    {shouldHideCitationBlock(latestResponse) ? (
                      <article className="assistant-history-item">
                        <h3>Sources</h3>
                        <p>{getEmptyCitationMessage(latestResponse)}</p>
                      </article>
                    ) : (
                      <article className="assistant-history-item">
                        <h3>Sources ({latestCitations.length})</h3>
                        {latestCitations.length === 0 ? (
                          <p>{getEmptyCitationMessage(latestResponse)}</p>
                        ) : (
                          <ul className="compact-citations">
                            {latestCitations.map((citation) => {
                              const href = buildCitationHref(citation, latestEntry.mode);
                              return (
                                <li key={`${latestEntry.id}-${citation.sourceIndex}`}>
                                  <span>[{citation.sourceIndex}]</span>
                                  {href ? (
                                    <a href={href} rel="noreferrer" target="_blank">{citation.title}</a>
                                  ) : (
                                    <strong>{citation.title}</strong>
                                  )}
                                </li>
                              );
                            })}
                          </ul>
                        )}
                      </article>
                    )}
                  </div>
                ) : null}

                <section className="diagnostics-shell compact-pane embedded-diagnostics">
                  <div className="diagnostics-header">
                    <p className="eyebrow">Debug view</p>
                    <h2>Latest retrieval details</h2>
                  </div>
                  <RetrievalDetailsPanel
                    response={latestEntry?.response}
                    requestedMode={latestEntry?.retrievalMode ?? retrievalMode}
                    requesterMode={latestEntry?.mode ?? mode}
                    errorMessage={latestEntry?.error}
                  />
                </section>
              </section>
            </aside>
          </div>
        </section>
      </main>
    </div>
  );
}

export default App;
