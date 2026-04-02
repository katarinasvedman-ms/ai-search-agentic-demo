# SecureRagChat Web

React + Vite + TypeScript frontend for the SecureRagChat demo. The UI is designed to replace a keyword-search experience with a natural-language assistant that makes query-time access control visible through grounded answers, citations, and diagnostics.

## What the demo shows

- Anonymous users can ask natural-language questions and get answers from public retrieval.
- Authenticated users can click `Sign in with Microsoft` to unlock entitled retrieval.
- The same prompt can produce different grounded answers depending on identity.
- Every answer shows diagnostics for retrieval plane, retrieval source, chunk count, and correlation ID.

## Local development

1. Start the backend from the sibling `SecureRagChat` project.
2. In this folder, install dependencies if needed:

```bash
npm install
```

3. Start the Vite dev server:

```bash
npm run dev
```

By default the app runs on `http://localhost:5173` and proxies `/api/*` requests to `https://localhost:7113`.

## Environment variables

Copy `.env.example` to `.env.local` if you want to customize the backend target.

- `VITE_BACKEND_PROXY_TARGET`: backend origin for the Vite dev proxy
- `VITE_API_BASE_URL`: optional explicit API base URL to bypass the proxy
- `VITE_AZURE_TENANT_ID`: Entra tenant ID used for sign-in authority
- `VITE_AZURE_CLIENT_ID`: frontend SPA app registration client ID
- `VITE_AZURE_API_SCOPE`: API scope to request for backend calls (example: `api://<backend-app-id>/access_as_user`)

For local development, keeping `VITE_API_BASE_URL` empty is usually best so the proxy handles HTTPS and origin differences.

If those three auth variables are not set, the UI can still run in authenticated mode when the backend is configured for development identity fallback.

For Microsoft sign-in, you must configure the SPA app registration redirect URI to include `http://localhost:5173`.

## Demo workflow

1. Run a query in `Anonymous` mode and observe `Public` retrieval.
2. Click `Sign in with Microsoft`, switch to `Authenticated`, and re-run the same query.
3. Compare the answer, citations, retrieval plane, and retrieval source.
4. Use prompts such as:
   - `What can Radio 4458 do?`
   - `How do I replace the battery in Radio 4458?`
   - `Show me the user manual for Radio 4458.`

## Notes

- The frontend acquires access tokens via MSAL popup sign-in and silent token refresh.
- The frontend never stores secrets beyond in-memory/session UI state.
- The backend remains the authority for auth detection, retrieval routing, and grounded response generation.
