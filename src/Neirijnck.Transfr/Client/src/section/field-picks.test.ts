import { describe, expect, it } from 'vitest';
import type { FieldDiffResponseModel, PropertyDiffResponseModel } from '../api/index.js';
import {
  batchButtonLabel,
  batchHint,
  describeFields,
  hasPick,
  nameField,
  propertyField,
  samePicks,
  togglePick,
} from './field-picks.js';

const title = { kind: 'Value', alias: 'title', culture: null, segment: null };
const nameEn = { kind: 'Name', culture: 'en-US', segment: null };

describe('field picks', () => {
  it('picks a field and drops it again, matching cultures without case', () => {
    const picked = togglePick([], nameEn);
    expect(picked).toEqual([nameEn]);
    expect(hasPick(picked, { kind: 'Name', culture: 'EN-us' })).toBe(true);
    expect(togglePick(picked, { kind: 'Name', culture: 'en-us', segment: null })).toEqual([]);
  });

  it('tells fields apart by kind, alias and culture', () => {
    expect(hasPick([title], { kind: 'Value', alias: 'title', culture: 'en-US' })).toBe(false);
    expect(hasPick([title], { kind: 'Value', alias: 'body' })).toBe(false);
    expect(hasPick([nameEn], { kind: 'Value', alias: null, culture: 'en-US' })).toBe(false);
  });

  it('compares lists of fields in any order', () => {
    expect(samePicks([title, nameEn], [nameEn, title])).toBe(true);
    expect(samePicks([title], [title, nameEn])).toBe(false);
    expect(samePicks([], [])).toBe(true);
  });

  it('offers only a name from the node box, and a property as its whole value', () => {
    const field = (kind: string): FieldDiffResponseModel => ({ kind, culture: 'nl-BE', segment: null, differs: true });
    expect(nameField(field('Name'))).toEqual({ kind: 'Name', culture: 'nl-BE', segment: null });
    expect(nameField(field('State'))).toBeUndefined();
    expect(nameField(field('DocumentType'))).toBeUndefined();
    expect(nameField(field('Template'))).toBeUndefined();

    const property: PropertyDiffResponseModel = {
      alias: 'blocks',
      label: 'Blocks',
      value: { differs: true, blocks: [] },
    } as unknown as PropertyDiffResponseModel;
    expect(propertyField(property)).toEqual({ kind: 'Value', alias: 'blocks', culture: null, segment: null });
  });

  it('names the fields a row carries over', () => {
    expect(
      describeFields([
        { kind: 'Value', alias: 'title', label: 'Title' },
        { kind: 'Value', alias: 'teaser', label: null },
        { kind: 'Name', culture: 'en-US' },
      ]),
    ).toBe('Title, teaser, Name (en-US)');
  });

  it('hints what to do while the ticks match the batch', () => {
    expect(batchHint(0)).toBe('Tick the fields to copy only those.');
    expect(batchHint(1)).toBe('1 field is in the batch. Change the ticks to update it.');
    expect(batchHint(3)).toBe('3 fields are in the batch. Change the ticks to update it.');
  });

  it.each([
    [0, 0, 'Add to the batch'],
    [0, 1, 'Add 1 field to the batch'],
    [0, 3, 'Add 3 fields to the batch'],
    [2, 0, 'Remove from the batch'],
    [2, 1, 'Keep 1 field in the batch'],
  ])('labels the batch button from %i to %i fields', (before, now, expected) => {
    expect(batchButtonLabel(before, now)).toBe(expected);
  });
});
