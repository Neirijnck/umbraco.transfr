import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      entry: 'src/bundle.manifests.ts', // Bundle registers one or more manifests
      formats: ['es'],
      fileName: 'transfr',
    },
    outDir: '../wwwroot/App_Plugins/Transfr', // your web component will be saved in this location
    emptyOutDir: true,
    // No source maps: they made up a quarter of the package, TypeScript sources included. A library build in ES format
    // keeps its line breaks and indentation (only names are shortened), so the shipped code stays readable as it is.
    sourcemap: false,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
