import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { client } from '../api/client.gen.js';
import { TransfrService } from '../api/index.js';
import { manifests as section } from '../section/manifest.js';

export const onInit: UmbEntryPointOnInit = async (host, extensionRegistry) => {
  // Wire the generated API client into the backoffice auth (base URL, bearer token, refresh).
  const authContext = await host.getContext(UMB_AUTH_CONTEXT);
  if (!authContext) {
    console.warn('[Transfr] UMB_AUTH_CONTEXT not available; API client is not authenticated');
    return;
  }
  authContext.configureClient(client);

  // The UI exists only where remotes are configured or may be connected at runtime. Otherwise the server answers 404
  // (as it does for non-admins): the normal case on a deployed environment, so no error notification.
  const { data } = await tryExecute(host, TransfrService.getRemotes(), { disableNotifications: true });
  if (data && (data.allowRuntimeRemotes || data.remotes.length > 0)) {
    extensionRegistry.registerMany(section);
  }
};

export const onUnload: UmbEntryPointOnUnload = () => {};
