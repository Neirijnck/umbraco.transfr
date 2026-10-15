import { describe, expect, it } from 'vitest';
import type { PlanNoteResponseModel, PlanRowResponseModel } from '../api/index.js';
import { countOutcomes, describeNote, describePublish, outcomeLook } from './plan-view.js';

const row = (outcome: string, publishCultures: Array<string | null> = [], kind = 'Document'): PlanRowResponseModel => ({
  kind,
  key: 'k',
  name: 'About',
  outcome,
  notes: [],
  // The API sends null for invariant content although the generated type says string.
  publishCultures: publishCultures as Array<string>,
  differences: [],
  schemaProblems: [],
});

const note = (kind: string, extra: Partial<PlanNoteResponseModel> = {}): PlanNoteResponseModel => ({
  kind,
  blocks: false,
  canAddToSelection: false,
  ...extra,
});

describe('outcomeLook', () => {
  it('names every outcome, and a conflict as skipped', () => {
    expect(
      ['Create', 'Update', 'Unchanged', 'Conflict', 'Blocked'].map((outcome) => outcomeLook(outcome).label),
    ).toEqual(['Create', 'Update', 'Unchanged', 'Conflict: skipped', 'Blocked']);
  });

  it('never shows an unknown outcome as harmless', () => {
    expect(outcomeLook('Delete')).toMatchObject({ label: 'Delete', color: 'danger' });
  });
});

describe('countOutcomes', () => {
  it('counts rows per outcome and leaves out the rest', () => {
    expect([...countOutcomes([row('Create'), row('Unchanged'), row('Create')])]).toEqual([
      ['Create', 2],
      ['Unchanged', 1],
    ]);
  });
});

describe('describePublish', () => {
  it.each([
    [row('Create', [null]), 'Published'],
    [row('Conflict', ['en-US', 'nl-BE']), 'Published in en-US, nl-BE'],
    [row('Create', []), 'Created as a draft, not published'],
    [row('Update', []), 'Saved, not published'],
  ])('describes %o', (input, expected) => {
    expect(describePublish(input)).toBe(expected);
  });

  it('says nothing for a row that is not written, or for media', () => {
    expect(describePublish(row('Unchanged', [null]))).toBeUndefined();
    expect(describePublish(row('Blocked', [null]))).toBeUndefined();
    expect(describePublish(row('Create', [], 'Media'))).toBeUndefined();
  });
});

describe('describeNote', () => {
  it.each([
    [note('ParentMissingOnTarget', { name: 'News' }), 'Its parent "News" isn\'t on B.'],
    [
      note('MissingReference', {
        name: 'About',
        propertyAlias: 'related',
        propertyName: 'Related page',
        culture: 'nl-BE',
      }),
      '"Related page" (nl-BE) links to "About", which isn\'t on B.',
    ],
    [
      note('MissingReference', { name: 'About', propertyAlias: 'related' }),
      '"related" links to "About", which isn\'t on B.',
    ],
    [note('SameNameOtherKey', { name: 'Contact' }), 'B has another node called "Contact" under the same parent.'],
    [
      note('DraftOnly', { culture: 'nl-BE' }),
      'nl-BE has unpublished changes here: the draft is copied, not published.',
    ],
    [note('DraftOnly'), 'The content has unpublished changes here: the draft is copied, not published.'],
    [note('UnderAnotherParentOnTarget', { name: 'News' }), 'It lives under "News" on B: updated there, not moved.'],
    [
      note('PlacedAfter', { key: 'k', name: 'News' }),
      'It takes its place here among its siblings on B: right after "News".',
    ],
    [note('PlacedAfter'), 'It takes its place here among its siblings on B: first.'],
    [
      note('ParentNotPublished', { name: 'News' }),
      'Its parent "News" isn\'t published on B: it is saved as a draft, not published.',
    ],
    [note('SomethingNew'), 'SomethingNew'],
  ])('describes %o', (input, expected) => {
    expect(describeNote(input, 'B')).toBe(expected);
  });
});
