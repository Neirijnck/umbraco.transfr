export const manifests: Array<UmbExtensionManifest> = [
  {
    name: 'Transfr Entrypoint',
    alias: 'Transfr.Entrypoint',
    type: 'backofficeEntryPoint',
    js: () => import('./entrypoint.js'),
  },
];
