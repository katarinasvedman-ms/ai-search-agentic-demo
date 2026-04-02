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
import type { RetrievalMode, TranscriptEntry } from './types';
import './App.css';

function App() {
  const [mode, setMode] = useState<'anonymous' | 'authenticated'>('anonymous');
  const [retrievalMode, setRetrievalMode] = useState<RetrievalMode>('Traditional');
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

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="identity-row">
          <span className={`identity-badge ${mode}`}>{mode === 'anonymous' ? 'Guest' : 'Azure user'}</span>
          <button
            className="identity-button"
            type="button"
            disabled={authBusy}
            onClick={mode === 'anonymous' ? handleSignIn : handleSignOut}
          >
            {authBusy ? 'Working...' : mode === 'anonymous' ? 'Log in' : 'Log out'}
          </button>
        </div>
      </header>

      <main className="chat-layout">
        <section className="composer-section">
          <h1>How can I help you today?</h1>

          <form className="composer-form" onSubmit={handleSubmit}>
            <div className="retrieval-toggle" role="group" aria-label="Retrieval mode">
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

            <div className="chat-input-shell">
              <input
                className="query-input"
                maxLength={4000}
                placeholder="Ask anything"
                type="text"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
              <button className="submit-button" type="submit">
                Ask
              </button>
            </div>
          </form>
        </section>

        <section className="transcript-section">
          {entries.length === 0 ? null : (
            <div className="transcript-debug-layout">
              <div className="transcript-list">
                {entries.map((entry) => (
                  <article key={entry.id} className="transcript-entry">
                    <div className="user-bubble">
                      <span className="message-meta">{entry.mode === 'anonymous' ? 'Guest' : 'Azure user'}</span>
                      <p>{entry.query}</p>
                    </div>

                    <div className="assistant-card">
                      {entry.isLoading ? (
                        <div className="loading-state">
                          <span className="loading-dot" />
                          Retrieving authorized context and generating a grounded answer...
                        </div>
                      ) : entry.error ? (
                        <div className="error-state">{entry.error}</div>
                      ) : entry.response ? (
                        <>
                          <div className="answer-block">
                            <p>{entry.response.answer}</p>
                          </div>

                          <div className="citation-block">
                            {entry.response.citations.length === 0 ? (
                              <p className="citation-empty">No citations were returned for this answer.</p>
                            ) : (
                              <ul className="citation-list">
                                {entry.response.citations.map((citation) => (
                                  <li key={`${entry.id}-${citation.sourceIndex}`}>
                                    <span className="citation-index">[{citation.sourceIndex}]</span>
                                    <div>
                                      <strong>{citation.title}</strong>
                                      {citation.url ? (
                                        <a href={citation.url} rel="noreferrer" target="_blank">
                                          {citation.url}
                                        </a>
                                      ) : null}
                                    </div>
                                  </li>
                                ))}
                              </ul>
                            )}
                          </div>
                        </>
                      ) : null}
                    </div>
                  </article>
                ))}
              </div>

              <aside className="debug-section" aria-label="Debug log">
                <div className="debug-section-header">
                  <h2>Debug</h2>
                </div>

                <div className="debug-list">
                  {entries.map((entry) => (
                    <article key={`debug-${entry.id}`} className="debug-entry">
                      <span className="debug-entry-title">
                        {entry.retrievalMode} · {entry.mode === 'anonymous' ? 'Guest' : 'Azure user'}
                      </span>
                      <p className="debug-entry-query">{entry.query}</p>
                      <RetrievalDetailsPanel
                        response={entry.response}
                        requestedMode={entry.retrievalMode}
                        requesterMode={entry.mode}
                        errorMessage={entry.error}
                      />
                    </article>
                  ))}
                </div>
              </aside>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

export default App;
