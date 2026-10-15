import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { PlanSelectionRequestModel, TransferResponseModel } from '../api/index.js';

export type TransfrCopyPlanModalData = {
  /** The remote to copy to. */
  alias: string;
  remoteName: string;
  /** The nodes picked in the tree. */
  selection: Array<PlanSelectionRequestModel>;
  /**
   * Called as soon as a transfer has run, however the panel is closed afterwards (Escape rejects the modal and drops
   * its value), so the tree can read the remote again and empty a completed batch.
   */
  onTransferred?: (transfer: TransferResponseModel) => void;
};

export type TransfrCopyPlanModalValue = {
  /**
   * The selection as it was when the panel closed: nodes added from a note are in it; empty after a transfer that
   * completed (the batch is done).
   */
  selection: Array<PlanSelectionRequestModel>;
};

export const TRANSFR_COPY_PLAN_MODAL = new UmbModalToken<TransfrCopyPlanModalData, TransfrCopyPlanModalValue>(
  'Transfr.Modal.CopyPlan',
  { modal: { type: 'sidebar', size: 'large' } },
);
