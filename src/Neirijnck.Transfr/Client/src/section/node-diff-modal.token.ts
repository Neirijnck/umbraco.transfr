import { UmbModalToken } from '@umbraco-cms/backoffice/modal';

export type TransfrNodeDiffModalData = {
  /** The remote to compare with. */
  alias: string;
  remoteName: string;
  /** The document's key, the same on both sides. */
  key: string;
  /** The document's name, for the headline while the diff loads. */
  name: string;
};

export const TRANSFR_NODE_DIFF_MODAL = new UmbModalToken<TransfrNodeDiffModalData, never>('Transfr.Modal.NodeDiff', {
  modal: { type: 'sidebar', size: 'large' },
});
