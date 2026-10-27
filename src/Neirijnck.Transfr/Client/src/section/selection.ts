import type { FieldChoiceRequestModel, PlanSelectionRequestModel, TreeRowResponseModel } from '../api/index.js';
import type { Direction } from './direction.js';
import { describeItems } from './item-count.js';
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

/**
 * What is picked for a copy to one remote: nodes (in the Content view) and media items (in the Media view, picked
 * whole: with or without everything below a folder), each in the order picked.
 */
export type Batch = { readonly documents: Selection; readonly media: Selection };

export const EMPTY_BATCH: Batch = { documents: new Map(), media: new Map() };

export function isEmptyBatch(batch: Batch): boolean {
  return batch.documents.size === 0 && batch.media.size === 0;
}

/** The batch as the plan and the transfer take it: the nodes, then the media items (kind Media). */
export function batchToRequest(batch: Batch): Array<PlanSelectionRequestModel> {
  return [
    ...toRequest(batch.documents),
    ...[...batch.media].map(([key, pick]) => ({ key, includeDescendants: pick === true, kind: 'Media' })),
  ];
}

/** A batch from the request shape; an entry without a kind is a node (stored before media could be picked). */
export function batchFromRequest(selection: ReadonlyArray<PlanSelectionRequestModel>): Batch {
  return {
    documents: fromRequest(selection.filter((choice) => choice.kind !== 'Media')),
    media: new Map(
      selection.filter((choice) => choice.kind === 'Media').map((choice) => [choice.key, choice.includeDescendants]),
    ),
  };
}

/** The part of the Web Storage API the batch needs (sessionStorage in the browser, a fake in tests). */
export type BatchStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

// A copy to the remote keeps the key it had before copies could go the other way, so a batch picked then is kept.
const batchKey = (alias: string, direction: Direction) =>
  direction === 'FromRemote' ? `transfr.batch.${alias}.from` : `transfr.batch.${alias}`;

/**
 * The batch picked for a remote in this browser tab: one per tab, per remote and per direction, nodes and media
 * together. Storage that is missing, full or blocked, or holds something else, gives an empty batch: a lost batch is
 * picked again, never a crash. An entry it can't read (another kind, unreadable fields) is left out.
 */
export function loadBatch(storage: BatchStorage | undefined, alias: string, direction: Direction = 'ToRemote'): Batch {
  try {
    const parsed: unknown = JSON.parse(storage?.getItem(batchKey(alias, direction)) ?? '[]');
    if (!Array.isArray(parsed)) {
      return EMPTY_BATCH;
    }
    return batchFromRequest(
      parsed.filter(
        (choice): choice is PlanSelectionRequestModel =>
          typeof choice?.key === 'string' &&
          typeof choice?.includeDescendants === 'boolean' &&
          (choice.kind === undefined ||
            choice.kind === null ||
            choice.kind === 'Document' ||
            choice.kind === 'Media') &&
          (choice.fields === undefined ||
            choice.fields === null ||
            (choice.kind !== 'Media' &&
              Array.isArray(choice.fields) &&
              choice.fields.every((field: unknown) => typeof (field as { kind?: unknown })?.kind === 'string'))),
      ),
    );
  } catch (error) {
    console.warn('[Transfr] The batch in session storage could not be read', error);
    return EMPTY_BATCH;
  }
}

/** Keeps the batch for a remote and direction in this browser tab; an empty batch is removed. */
export function saveBatch(
  storage: BatchStorage | undefined,
  alias: string,
  batch: Batch,
  direction: Direction = 'ToRemote',
): void {
  try {
    if (isEmptyBatch(batch)) {
      storage?.removeItem(batchKey(alias, direction));
    } else {
      storage?.setItem(batchKey(alias, direction), JSON.stringify(batchToRequest(batch)));
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

/** E.g. "3 nodes and 1 media item selected, 1 with everything below it, 1 with only some fields". */
export function describeBatch(batch: Batch): string {
  const picks = [...batch.documents.values(), ...batch.media.values()];
  const below = picks.filter((pick) => pick === true).length;
  const fields = picks.filter((pick) => !isWhole(pick)).length;
  const parts = [`${describeItems(batch.documents.size, batch.media.size)} selected`];
  if (below > 0) {
    parts.push(`${below} with everything below ${below === 1 ? 'it' : 'them'}`);
  }
  if (fields > 0) {
    parts.push(`${fields} with only some fields`);
  }
  return parts.join(', ');
}

/**
 * Why a row's node can't be picked for a copy, or undefined when it can: it must be on the source in this place (here
 * when copying to the remote, on the remote when copying from it; not only on the other side, not a placeholder for its
 * place elsewhere), and not blocked by the schema or a blocked parent.
 */
export function whyNotSelectable(
  row: TreeRowResponseModel,
  block: RowBlock | undefined,
  refused: boolean,
  direction: Direction = 'ToRemote',
): string | undefined {
  const fromRemote = direction === 'FromRemote';
  if (refused) {
    return `The remote was installed separately: nothing can be copied ${fromRemote ? 'from' : 'to'} it.`;
  }
  if (row.status === (fromRemote ? 'OnlyLocal' : 'OnlyRemote')) {
    return fromRemote ? 'It is not on the remote.' : 'It is not here.';
  }
  if (row.status === (fromRemote ? 'ElsewhereOnRemote' : 'ElsewhereLocally')) {
    return fromRemote
      ? 'On the remote it lives under another parent: select it there.'
      : 'Here it lives under another parent: select it there.';
  }
  if (block) {
    return block.reason;
  }
  return undefined;
}
