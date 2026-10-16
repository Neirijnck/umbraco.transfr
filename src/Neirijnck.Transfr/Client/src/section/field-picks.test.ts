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
import type { FieldPick } from './field-picks.js';

// Every property set, as the API types of both lines require (Umbraco 18 makes nullable fields required).
const field = (kind: string, alias: string | null = null, culture: string | null = null): FieldPick => ({
  kind,
  alias,
  culture,
  segment: null,
});
const title = field('Value', 'title');
const nameEn = field('Name', null, 'en-US');

describe('field picks', () => {
  it('picks a field and drops it again, matching cultures without case', () => {
    const picked = togglePick([], nameEn);
    expect(picked).toEqual([nameEn]);
    expect(hasPick(picked, field('Name', null, 'EN-us'))).toBe(true);
    expect(togglePick(picked, field('Name', null, 'en-us'))).toEqual([]);
  });

  it('treats a property left out like null', () => {
    expect(hasPick([nameEn], { kind: 'Name', culture: 'en-US' } as FieldPick)).toBe(true);
  });

  it('tells fields apart by kind, alias and culture', () => {
    expect(hasPick([title], field('Value', 'title', 'en-US'))).toBe(false);
    expect(hasPick([title], field('Value', 'body'))).toBe(false);
    expect(hasPick([nameEn], field('Value', null, 'en-US'))).toBe(false);
  });

  it('compares lists of fields in any order', () => {
    expect(samePicks([title, nameEn], [nameEn, title])).toBe(true);
    expect(samePicks([title], [title, nameEn])).toBe(false);
    expect(samePicks([], [])).toBe(true);
  });

  it('offers only a name from the node box, and a property as its whole value', () => {
    const row = (kind: string): FieldDiffResponseModel => ({
      kind,
      culture: 'nl-BE',
      segment: null,
      local: 'a',
      remote: 'b',
      differs: true,
    });
    expect(nameField(row('Name'))).toEqual({ kind: 'Name', alias: null, culture: 'nl-BE', segment: null });
    expect(nameField(row('State'))).toBeUndefined();
    expect(nameField(row('DocumentType'))).toBeUndefined();
    expect(nameField(row('Template'))).toBeUndefined();

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
        { kind: 'Value', alias: 'title', label: 'Title', culture: null, segment: null },
        { kind: 'Value', alias: 'teaser', label: null, culture: null, segment: null },
        { kind: 'Name', alias: null, label: null, culture: 'en-US', segment: null },
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
