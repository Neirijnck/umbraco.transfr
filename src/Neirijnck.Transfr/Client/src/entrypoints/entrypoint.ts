import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { client } from '../api/client.gen.js';

export const onInit: UmbEntryPointOnInit = async (host, _extensionRegistry) => {
  // Wire the generated API client into the backoffice auth (base URL, bearer token, refresh).
  const authContext = await host.getContext(UMB_AUTH_CONTEXT);
  if (!authContext) {
    console.warn('[Transfr] UMB_AUTH_CONTEXT not available; API client is not authenticated');
    return;
  }
  authContext.configureClient(client);
};

export const onUnload: UmbEntryPointOnUnload = () => {};
