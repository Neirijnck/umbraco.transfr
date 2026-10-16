import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { FieldChoiceRequestModel } from '../api/index.js';

export type TransfrNodeDiffModalData = {
  /** The remote to compare with. */
  alias: string;
  remoteName: string;
  /** The document's key, the same on both sides. */
  key: string;
  /** The document's name, for the headline while the diff loads. */
  name: string;
  /**
   * The node in the batch, when fields can be picked for it (from the compare tree, for a node that can be copied):
   * whole (then its fields all come along already) or the fields picked so far. Absent = only the diff, read-only.
   */
  batch?: { whole: boolean; fields: Array<FieldChoiceRequestModel> };
};

export type TransfrNodeDiffModalValue = {
  /** The fields picked for the node when the panel was handed back (none = the node leaves the batch). */
  fields: Array<FieldChoiceRequestModel>;
};

export const TRANSFR_NODE_DIFF_MODAL = new UmbModalToken<TransfrNodeDiffModalData, TransfrNodeDiffModalValue>(
  'Transfr.Modal.NodeDiff',
  {
    modal: { type: 'sidebar', size: 'large' },
  },
);
