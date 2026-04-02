import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
  type AuthenticationResult,
} from '@azure/msal-browser';

const tenantId = import.meta.env.VITE_AZURE_TENANT_ID?.trim() ?? '';
const clientId = import.meta.env.VITE_AZURE_CLIENT_ID?.trim() ?? '';
const apiScope = import.meta.env.VITE_AZURE_API_SCOPE?.trim() ?? '';

const isConfigured = Boolean(tenantId && clientId && apiScope);

const msalClient = isConfigured
  ? new PublicClientApplication({
      auth: {
        clientId,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        redirectUri: window.location.origin,
      },
      cache: {
        cacheLocation: 'sessionStorage',
      },
    })
  : null;

const authRequest = {
  scopes: isConfigured ? [apiScope] : [],
};

let initialized = false;

async function ensureInitialized(): Promise<void> {
  if (!msalClient || initialized) {
    return;
  }

  await msalClient.initialize();
  initialized = true;
}

function getActiveAccount(): AccountInfo | null {
  if (!msalClient) {
    return null;
  }

  const active = msalClient.getActiveAccount();
  if (active) {
    return active;
  }

  const account = msalClient.getAllAccounts()[0] ?? null;
  if (account) {
    msalClient.setActiveAccount(account);
  }

  return account;
}

export function getAuthConfigurationState(): { isConfigured: boolean; missing: string[] } {
  const missing: string[] = [];

  if (!tenantId) {
    missing.push('VITE_AZURE_TENANT_ID');
  }

  if (!clientId) {
    missing.push('VITE_AZURE_CLIENT_ID');
  }

  if (!apiScope) {
    missing.push('VITE_AZURE_API_SCOPE');
  }

  return { isConfigured, missing };
}

export async function signInWithMicrosoft(): Promise<AccountInfo> {
  if (!msalClient) {
    throw new Error('Microsoft sign-in is not configured. Set VITE_AZURE_TENANT_ID, VITE_AZURE_CLIENT_ID, and VITE_AZURE_API_SCOPE.');
  }

  await ensureInitialized();

  const result = (await msalClient.loginPopup(authRequest)) as AuthenticationResult;
  msalClient.setActiveAccount(result.account);

  if (!result.account) {
    throw new Error('Sign-in did not return an account.');
  }

  return result.account;
}

export async function signOutMicrosoft(): Promise<void> {
  if (!msalClient) {
    return;
  }

  await ensureInitialized();

  const account = getActiveAccount();
  if (!account) {
    return;
  }

  await msalClient.logoutPopup({ account });
}

export async function getAccessToken(): Promise<string> {
  if (!msalClient) {
    throw new Error('Microsoft sign-in is not configured. Set VITE_AZURE_TENANT_ID, VITE_AZURE_CLIENT_ID, and VITE_AZURE_API_SCOPE.');
  }

  await ensureInitialized();

  const account = getActiveAccount();
  if (!account) {
    throw new Error('You are not signed in. Click Sign in first.');
  }

  try {
    const silent = await msalClient.acquireTokenSilent({
      ...authRequest,
      account,
    });

    return silent.accessToken;
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError)) {
      throw error;
    }

    const interactive = await msalClient.acquireTokenPopup({
      ...authRequest,
      account,
    });

    return interactive.accessToken;
  }
}

export async function getSignedInAccount(): Promise<AccountInfo | null> {
  if (!msalClient) {
    return null;
  }

  await ensureInitialized();
  return getActiveAccount();
}
