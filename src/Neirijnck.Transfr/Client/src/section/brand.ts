import { css } from '@umbraco-cms/backoffice/external/lit';

/**
 * Transfr's own colours, taken from the package icon (src/Neirijnck.Transfr/icon.png): blue for this environment and
 * Transfr's own banners, teal for the remote, amber for picking nodes (the icon's action button). They mark identity
 * and Transfr's own controls only. Status colours (only here, moved, changed, blocked, failed) stay the backoffice's
 * own, so they read the same as everywhere else in Umbraco.
 */
export const brandStyles = css`
  :host {
    --transfr-color-brand: #1565c0;
    --transfr-color-local: var(--transfr-color-brand);
    --transfr-color-remote: #00897b;
    --transfr-color-accent: #ffb300;
  }
`;
