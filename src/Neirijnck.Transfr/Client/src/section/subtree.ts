import type { SubtreeLevelResponseModel, TreeRowResponseModel } from '../api/index.js';

// The root level has no parent key; '' stands for it, as in the compare view's level and expansion maps.
const ROOT = '';

/** Whether a row shows anything other than "the same, in the same place". */
export function hasDifference(row: TreeRowResponseModel): boolean {
  return (
    row.status !== 'InBoth' ||
    row.orderDiffers ||
    Boolean(row.sameNameKey) ||
    (row.content !== null && row.content !== undefined && row.content.status !== 'Identical')
  );
}

export type SubtreeCounts = {
  nodes: number;
  identical: number;
  changed: number;
  orderDiffers: number;
  onlyLocal: number;
  onlyRemote: number;
  elsewhere: number;
  failed: number;
};

/**
 * Counts the nodes of a compared subtree. A node under another parent on one side shows up in two levels; it counts
 * once.
 */
export function countSubtree(levels: Array<SubtreeLevelResponseModel>): SubtreeCounts {
  const byKey = new Map<string, Array<TreeRowResponseModel>>();
  for (const row of levels.flatMap((level) => level.rows)) {
    byKey.set(row.key, [...(byKey.get(row.key) ?? []), row]);
  }

  const counts: SubtreeCounts = {
    nodes: byKey.size,
    identical: 0,
    changed: 0,
    orderDiffers: 0,
    onlyLocal: 0,
    onlyRemote: 0,
    elsewhere: 0,
    failed: 0,
  };
  for (const rows of byKey.values()) {
    const status = rows[0]!.status;
    const changed = rows.some((row) => row.content?.status === 'Different');
    const failed = rows.some((row) => row.content?.status === 'Failed');
    const orderDiffers = rows.some((row) => row.orderDiffers);

    if (status === 'OnlyLocal') counts.onlyLocal++;
    else if (status === 'OnlyRemote') counts.onlyRemote++;
    else if (status !== 'InBoth') counts.elsewhere++;
    if (changed) counts.changed++;
    if (failed) counts.failed++;
    if (orderDiffers) counts.orderDiffers++;
    if (status === 'InBoth' && !changed && !failed && !orderDiffers) counts.identical++;
  }

  return counts;
}

/**
 * The rows to expand (as "parentKey/key" positions) so that every row with a difference is visible, up to the level the
 * subtree started at; the row it started from is the caller's to open. A node reached through two parents is opened
 * through the first one found.
 */
export function positionsToReveal(levels: Array<SubtreeLevelResponseModel>): Set<string> {
  const parentOf = new Map<string, string>();
  for (const level of levels) {
    for (const row of level.rows) {
      if (!parentOf.has(row.key)) {
        parentOf.set(row.key, level.parentKey ?? ROOT);
      }
    }
  }

  const positions = new Set<string>();
  for (const level of levels) {
    if (!level.rows.some(hasDifference)) {
      continue;
    }

    // Walk up until the level the subtree started at: its parent is not a row in the subtree.
    for (let key = level.parentKey ?? ROOT; key !== ROOT;) {
      const parent = parentOf.get(key);
      if (parent === undefined) {
        break;
      }
      positions.add(`${parent}/${key}`);
      key = parent;
    }
  }

  return positions;
}

/** A duration as people read it: milliseconds under a second, else seconds with one decimal. */
export function formatDuration(milliseconds: number): string {
  return milliseconds < 1000
    ? `${Math.round(milliseconds)} ms`
    : `${(milliseconds / 1000).toLocaleString('en', { maximumFractionDigits: 1 })} s`;
}

/** The outcome in one sentence, leaving out what didn't occur. */
export function describeCounts(counts: SubtreeCounts, remoteName: string): string {
  const parts = [
    [counts.changed, 'changed'],
    [counts.orderDiffers, 'in another order'],
    [counts.onlyLocal, 'only here'],
    [counts.onlyRemote, `only on ${remoteName}`],
    [counts.elsewhere, 'under another parent'],
    [counts.failed, 'not compared'],
  ] as const;
  const found = parts.filter(([count]) => count > 0).map(([count, what]) => `${count} ${what}`);
  const nodes = `${counts.nodes} ${counts.nodes === 1 ? 'node' : 'nodes'}`;
  return found.length === 0 ? `${nodes}, all identical.` : `${nodes}: ${found.join(', ')}.`;
}
