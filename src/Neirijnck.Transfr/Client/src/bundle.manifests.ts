import { manifests as entrypoints } from './entrypoints/manifest.js';

// Collates the manifests registered up front; loaded via the bundle in public/umbraco-package.json.
// The section is registered by the entry point, and only when remotes are configured.
export const manifests: Array<UmbExtensionManifest> = [...entrypoints];
