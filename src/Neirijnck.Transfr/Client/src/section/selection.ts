import type { FieldChoiceRequestModel, PlanSelectionRequestModel, TreeRowResponseModel } from '../api/index.js';
import type { RowBlock } from './schema-view.js';

/**
 * How a node is picked: whole (true = with everything below it, false = this node only), or only some of its fields
 * (picked in its diff). The whole node wins: picking it whole drops its fields.
 */
export type NodePick = boolean | ReadonlyArray<FieldChoiceRequestModel>;

/** The nodes picked for a copy, by key, in the order they were picked. */
export type Selection = ReadonlyMap<string, NodePick>;

/** Whether a node is picked whole (not: not picked, or picked by its fields). */
export function isWhole(pick: NodePick | undefined): pick is boolean {
  return typeof pick === 'boolean';
}

/** Picks a node whole (on its own) or drops it; a node picked by its fields becomes whole. */
export function toggleSelected(selection: Selection, key: string): Selection {
  const next = new Map(selection);
  if (isWhole(next.get(key))) {
    next.delete(key);
  } else {
    next.set(key, false);
  }
  return next;
}

/** Whether everything below a node picked whole comes along; any other node is left alone. */
export function setIncludeDescendants(selection: Selection, key: string, include: boolean): Selection {
  if (!isWhole(selection.get(key))) {
    return selection;
  }
  return new Map(selection).set(key, include);
}

/** Picks these nodes too, whole (on their own); nodes already picked whole keep their choice. */
export function addToSelection(selection: Selection, keys: Iterable<string>): Selection {
  const next = new Map(selection);
  for (const key of keys) {
    if (!isWhole(next.get(key))) {
      next.set(key, false);
    }
  }
  return next;
}

/** Picks these fields of a node (none = drops it); a node picked whole keeps that, since the whole node wins. */
export function setFields(
  selection: Selection,
  key: string,
  fields: ReadonlyArray<FieldChoiceRequestModel>,
): Selection {
  if (isWhole(selection.get(key))) {
    return selection;
  }
  const next = new Map(selection);
  if (fields.length === 0) {
    next.delete(key);
  } else {
    next.set(key, [...fields]);
  }
  return next;
}

export function toRequest(selection: Selection): Array<PlanSelectionRequestModel> {
  return [...selection].map(([key, pick]) =>
    isWhole(pick) ? { key, includeDescendants: pick } : { key, includeDescendants: false, fields: [...pick] },
  );
}

export function fromRequest(selection: ReadonlyArray<PlanSelectionRequestModel>): Selection {
  return new Map(
    selection.map((choice): [string, NodePick] => [
      choice.key,
      choice.fields && choice.fields.length > 0 ? [...choice.fields] : choice.includeDescendants,
    ]),
  );
}

/** The part of the Web Storage API the batch needs (sessionStorage in the browser, a fake in tests). */
export type BatchStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

const batchKey = (alias: string) => `transfr.batch.${alias}`;

/**
 * The batch picked for a remote in this browser tab: one per tab and per remote. Storage that is missing,
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
          typeof choice?.key === 'string' &&
          typeof choice?.includeDescendants === 'boolean' &&
          (choice.fields === undefined ||
            choice.fields === null ||
            (Array.isArray(choice.fields) &&
              choice.fields.every((field: unknown) => typeof (field as { kind?: unknown })?.kind === 'string'))),
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

/** E.g. "3 nodes selected, 1 with everything below it, 1 with only some fields". */
export function describeSelection(selection: Selection): string {
  const count = selection.size;
  const below = [...selection.values()].filter((pick) => pick === true).length;
  const fields = [...selection.values()].filter((pick) => !isWhole(pick)).length;
  const parts = [`${count} ${count === 1 ? 'node' : 'nodes'} selected`];
  if (below > 0) {
    parts.push(`${below} with everything below ${below === 1 ? 'it' : 'them'}`);
  }
  if (fields > 0) {
    parts.push(`${fields} with only some fields`);
  }
  return parts.join(', ');
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
