import type {
  DryRunRowRequestModel,
  PlanDriftResponseModel,
  PlanRowResponseModel,
  TransferResponseModel,
} from '../api/index.js';
import { countByKind, describeItems } from './item-count.js';
import type { OutcomeLook } from './plan-view.js';

/** The rows of a dry run (documents and media) as the transfer sends them back, to be checked against a new plan. */
export function toDryRun(rows: ReadonlyArray<PlanRowResponseModel>): Array<DryRunRowRequestModel> {
  return rows.map((row) => ({ key: row.key, outcome: row.outcome, fingerprint: row.fingerprint ?? null }));
}

/** The rows a transfer would write: creates, updates, and the conflicts chosen to overwrite (nodes and media). */
function writtenRows(
  rows: ReadonlyArray<PlanRowResponseModel>,
  overwrite: ReadonlySet<string>,
): Array<PlanRowResponseModel> {
  return rows.filter(
    (row) =>
      row.outcome === 'Create' || row.outcome === 'Update' || (row.outcome === 'Conflict' && overwrite.has(row.key)),
  );
}

/** How many nodes and media items a transfer would write. */
export function countWrites(rows: ReadonlyArray<PlanRowResponseModel>, overwrite: ReadonlySet<string>): number {
  return writtenRows(rows, overwrite).length;
}

/** What a transfer would write, in words, e.g. "2 nodes and 1 media item". */
export function describeWrites(rows: ReadonlyArray<PlanRowResponseModel>, overwrite: ReadonlySet<string>): string {
  const { nodes, media } = countByKind(writtenRows(rows, overwrite));
  return describeItems(nodes, media);
}

/** How a row's result is shown. An unknown result (a newer server) is shown as it comes, never as harmless. */
export function resultLook(result: string): OutcomeLook {
  switch (result) {
    case 'Written':
      return { label: 'Written', color: 'positive', look: 'primary', icon: 'icon-check' };
    case 'Unchanged':
      return { label: 'Unchanged', color: 'default', look: 'outline', icon: 'icon-check' };
    case 'Skipped':
      return { label: 'Skipped: conflict', color: 'warning', look: 'outline', icon: 'icon-alert' };
    case 'Blocked':
      return { label: 'Blocked', color: 'danger', look: 'outline', icon: 'icon-block' };
    case 'Failed':
      return { label: 'Failed', color: 'danger', look: 'primary', icon: 'icon-wrong' };
    case 'NotRun':
      return { label: 'Not run', color: 'default', look: 'outline', icon: 'icon-stop' };
    default:
      return { label: result, color: 'danger', look: 'outline', icon: 'icon-help-alt' };
  }
}

/** Why a transfer was refused for one node, in words. */
export function describeDrift(drift: PlanDriftResponseModel, remoteName: string): string {
  const name = drift.name ? `"${drift.name}"` : 'A node';
  switch (drift.reason) {
    case 'Saved':
      return `${name} was saved here or on ${remoteName} since the dry run.`;
    case 'OutcomeChanged':
      return `${name} would now be "${drift.now}" instead of "${drift.was}".`;
    case 'NotInDryRun':
      return `${name} is in the plan now, but wasn't in the dry run.`;
    case 'NoLongerPlanned':
      return 'A node from the dry run is no longer in the plan.';
    default:
      return `${name}: ${drift.reason}`;
  }
}

/** The headline of a transfer report. */
export function describeTransfer(response: TransferResponseModel, remoteName: string): string {
  const writtenRows = response.rows.filter((row) => row.result === 'Written');
  const written = writtenRows.length;
  const { nodes: writtenNodes, media: writtenMedia } = countByKind(writtenRows);
  const nodes = describeItems(writtenNodes, writtenMedia);
  const sorted =
    response.sortedParents > 0
      ? `, and put in order under ${response.sortedParents} ${response.sortedParents === 1 ? 'parent' : 'parents'}`
      : '';
  switch (response.status) {
    case 'Completed':
      return `Copied ${nodes} to ${remoteName}${sorted}.`;
    case 'Failed':
      return `Stopped at a failure. ${
        written === 0
          ? 'Nothing was copied.'
          : `${nodes} ${written === 1 ? 'was' : 'were'} copied before it and ${written === 1 ? 'stays' : 'stay'} copied.`
      }`;
    case 'Refused':
      return `Nothing was copied: something changed since the dry run. Run the dry run again and check it.`;
    case 'Busy':
      return `Nothing was copied: ${response.error ?? 'another transfer to this remote is running.'}`;
    default:
      return `The transfer ended with "${response.status}".`;
  }
}

/** Whether the remote may have changed: then the tree is read again. */
export function wroteAnything(response: TransferResponseModel): boolean {
  return response.rows.some((row) => row.result === 'Written' || row.result === 'Failed');
}
