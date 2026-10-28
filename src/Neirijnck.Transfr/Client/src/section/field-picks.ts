import type {
  FieldChoiceRequestModel,
  FieldChoiceResponseModel,
  FieldDiffResponseModel,
  PropertyDiffResponseModel,
} from '../api/index.js';
import { sidesOf } from './direction.js';
import type { Direction } from './direction.js';

/** One field picked in a node's diff for a field-level copy. */
export type FieldPick = FieldChoiceRequestModel;

const variantOf = (value: string | null | undefined) => (value ?? '').toLowerCase();

/** The same field: kind and alias, in the same culture and segment (compared the way cultures compare). */
export function samePick(left: FieldPick, right: FieldPick): boolean {
  return (
    left.kind === right.kind &&
    (left.alias ?? null) === (right.alias ?? null) &&
    variantOf(left.culture) === variantOf(right.culture) &&
    variantOf(left.segment) === variantOf(right.segment)
  );
}

export function hasPick(picks: ReadonlyArray<FieldPick>, pick: FieldPick): boolean {
  return picks.some((candidate) => samePick(candidate, pick));
}

/** Picks a field, or drops it when it was picked. */
export function togglePick(picks: ReadonlyArray<FieldPick>, pick: FieldPick): Array<FieldPick> {
  return hasPick(picks, pick) ? picks.filter((candidate) => !samePick(candidate, pick)) : [...picks, pick];
}

/** Whether two lists hold the same fields, in any order. */
export function samePicks(left: ReadonlyArray<FieldPick>, right: ReadonlyArray<FieldPick>): boolean {
  return left.length === right.length && left.every((pick) => hasPick(right, pick));
}

/** The field behind a row of the diff's node box: only a name can be picked (not the type, template or state). */
export function nameField(field: FieldDiffResponseModel): FieldPick | undefined {
  return field.kind === 'Name'
    ? { kind: 'Name', alias: null, culture: field.culture ?? null, segment: field.segment ?? null }
    : undefined;
}

/** The field behind a property of the diff: the whole value, blocks included. */
export function propertyField(property: PropertyDiffResponseModel): FieldPick {
  return { kind: 'Value', alias: property.alias, culture: property.culture ?? null, segment: property.segment ?? null };
}

/**
 * E.g. "Title, Name (en-US)": the fields a row carries over, by label. Takes picked fields too (no label: the alias),
 * hence the loose type.
 */
export function describeFields(
  fields: ReadonlyArray<Pick<FieldChoiceResponseModel, 'kind'> & Partial<Omit<FieldChoiceResponseModel, 'kind'>>>,
): string {
  return fields
    .map((field) => {
      const what = field.kind === 'Name' ? 'Name' : (field.label ?? field.alias ?? field.kind);
      const where = [field.culture, field.segment].filter(Boolean).join(', ');
      return where ? `${what} (${where})` : what;
    })
    .join(', ');
}

/**
 * What the panel says while the ticks match the batch, instead of a disabled button (a disabled primary button looks
 * the same as an enabled one, measured: only the cursor tells).
 */
export function batchHint(before: number): string {
  return before === 0
    ? 'Tick the fields to copy only those.'
    : `${before} ${before === 1 ? 'field is' : 'fields are'} in the batch. Change the ticks to update it.`;
}

/** The button that hands the picked fields to the batch, from what the batch held for the node before. */
export function batchButtonLabel(before: number, now: number): string {
  const fields = (count: number) => `${count} ${count === 1 ? 'field' : 'fields'}`;
  if (before === 0) {
    return now === 0 ? 'Add to the batch' : `Add ${fields(now)} to the batch`;
  }
  return now === 0 ? 'Remove from the batch' : `Keep ${fields(now)} in the batch`;
}

/** The accessible name of a field's tick: which field goes which way. */
export function pickLabel(label: string, direction: Direction, remoteName: string): string {
  return `Copy ${label} ${sidesOf(direction, remoteName).way}`;
}
