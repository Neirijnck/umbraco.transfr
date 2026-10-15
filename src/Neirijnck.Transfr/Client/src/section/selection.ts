import type { PlanSelectionRequestModel, TreeRowResponseModel } from '../api/index.js';
import type { RowBlock } from './schema-view.js';

/** The nodes picked for a copy: key to whether everything below it comes along. Kept in the order they were picked. */
export type Selection = ReadonlyMap<string, boolean>;

/** Picks a node (on its own) or drops it. */
export function toggleSelected(selection: Selection, key: string): Selection {
  const next = new Map(selection);
  if (!next.delete(key)) {
    next.set(key, false);
  }
  return next;
}

/** Whether everything below a picked node comes along; a node that isn't picked is left alone. */
export function setIncludeDescendants(selection: Selection, key: string, include: boolean): Selection {
  if (!selection.has(key)) {
    return selection;
  }
  return new Map(selection).set(key, include);
}

/** Picks these nodes too (on their own); nodes already picked keep their choice. */
export function addToSelection(selection: Selection, keys: Iterable<string>): Selection {
  const next = new Map(selection);
  for (const key of keys) {
    if (!next.has(key)) {
      next.set(key, false);
    }
  }
  return next;
}

export function toRequest(selection: Selection): Array<PlanSelectionRequestModel> {
  return [...selection].map(([key, includeDescendants]) => ({ key, includeDescendants }));
}

export function fromRequest(selection: ReadonlyArray<PlanSelectionRequestModel>): Selection {
  return new Map(selection.map((choice) => [choice.key, choice.includeDescendants]));
}

/** The part of the Web Storage API the batch needs (sessionStorage in the browser, a fake in tests). */
export type BatchStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

const batchKey = (alias: string) => `transfr.batch.${alias}`;

/**
 * The batch picked for a remote in this browser tab (Preben's call: per tab, per remote). Storage that is missing,
 * full or blocked, or holds something else, gives an empty batch: a lost batch is picked again, never a crash.
 */
export function loadBatch(storage: BatchStorage | undefined, alias: string): Selection {
  try {
    const parsed: unknown = JSON.parse(storage?.getItem(batchKey(alias)) ?? '[]');
    if (!Array.isArray(parsed)) {
      return new Map();
    }
    return fromRequest(
      parsed.filter(
        (choice): choice is PlanSelectionRequestModel =>
          typeof choice?.key === 'string' && typeof choice?.includeDescendants === 'boolean',
      ),
    );
  } catch (error) {
    console.warn('[Transfr] The batch in session storage could not be read', error);
    return new Map();
  }
}

/** Keeps the batch for a remote in this browser tab; an empty batch is removed. */
export function saveBatch(storage: BatchStorage | undefined, alias: string, selection: Selection): void {
  try {
    if (selection.size === 0) {
      storage?.removeItem(batchKey(alias));
    } else {
      storage?.setItem(batchKey(alias), JSON.stringify(toRequest(selection)));
    }
  } catch (error) {
    console.warn('[Transfr] The batch could not be kept in session storage', error);
  }
}

/**
 * The name of the picked ancestor that brings a node's children along (with everything below it), or undefined. `cover`
 * is the node's own: a covered node covers its children too.
 */
export function coverForChildren(
  selection: Selection,
  key: string,
  name: string,
  cover: string | undefined,
): string | undefined {
  return cover ?? (selection.get(key) === true ? name : undefined);
}

/** E.g. "3 nodes selected, 1 with everything below it". */
export function describeSelection(selection: Selection): string {
  const count = selection.size;
  const below = [...selection.values()].filter(Boolean).length;
  const nodes = `${count} ${count === 1 ? 'node' : 'nodes'} selected`;
  return below === 0 ? nodes : `${nodes}, ${below} with everything below ${below === 1 ? 'it' : 'them'}`;
}

/**
 * Why a row's node can't be picked for a copy from here, or undefined when it can: it must be here in this place (not
 * only on the remote, not a placeholder for its place elsewhere), and not blocked by the schema or a blocked parent.
 */
export function whyNotSelectable(
  row: TreeRowResponseModel,
  block: RowBlock | undefined,
  refused: boolean,
): string | undefined {
  if (refused) {
    return 'The remote was installed separately: nothing can be copied to it.';
  }
  if (row.status === 'OnlyRemote') {
    return 'This node is not here.';
  }
  if (row.status === 'ElsewhereLocally') {
    return 'Here this node lives under another parent: select it there.';
  }
  if (block) {
    return block.reason;
  }
  return undefined;
}
