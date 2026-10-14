import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { PlanSelectionRequestModel } from '../api/index.js';

export type TransfrCopyPlanModalData = {
  /** The remote to copy to. */
  alias: string;
  remoteName: string;
  /** The nodes picked in the tree. */
  selection: Array<PlanSelectionRequestModel>;
};

export type TransfrCopyPlanModalValue = {
  /** The selection as it was when the panel closed: nodes added from a note are in it. */
  selection: Array<PlanSelectionRequestModel>;
};

export const TRANSFR_COPY_PLAN_MODAL = new UmbModalToken<TransfrCopyPlanModalData, TransfrCopyPlanModalValue>(
  'Transfr.Modal.CopyPlan',
  { modal: { type: 'sidebar', size: 'large' } },
);
