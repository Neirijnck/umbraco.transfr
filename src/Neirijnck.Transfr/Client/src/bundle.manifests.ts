import { manifests as entrypoints } from './entrypoints/manifest.js';
import { manifests as section } from './section/manifest.js';

// Collates all manifests; loaded via the bundle in public/umbraco-package.json
export const manifests: Array<UmbExtensionManifest> = [...entrypoints, ...section];
