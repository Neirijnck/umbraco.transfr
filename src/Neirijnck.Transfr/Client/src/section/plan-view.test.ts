import { describe, expect, it } from 'vitest';
import type { PlanNoteResponseModel, PlanRowResponseModel } from '../api/index.js';
import {
  countOutcomes,
  describeLastCopy,
  describeNote,
  describePublish,
  describeRemoteChange,
  outcomeLook,
} from './plan-view.js';

const row = (outcome: string, publishCultures: Array<string | null> = [], kind = 'Document'): PlanRowResponseModel => ({
  kind,
  key: 'k',
  parentKey: null,
  name: 'About',
  documentTypeName: null,
  outcome,
  notes: [],
  // The API sends null for invariant content although the generated type says string.
  publishCultures: publishCultures as Array<string>,
  differences: [],
  schemaProblems: [],
  fingerprint: null,
  changedOnRemote: null,
  lastCopied: null,
  fields: null,
});

const note = (kind: string, extra: Partial<PlanNoteResponseModel> = {}): PlanNoteResponseModel => ({
  kind,
  blocks: false,
  key: null,
  name: null,
  propertyAlias: null,
  propertyName: null,
  culture: null,
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

describe('describeRemoteChange', () => {
  // A summer time in Brussels (UTC+2), as the remote sends it.
  const at = '2026-10-15T10:25:13.0861+00:00';

  it('names the change, the remote, the user and the time in the given zone', () => {
    expect(describeRemoteChange({ by: 'Transfr Remote', at, what: 'Save' }, 'Test site B', 'Europe/Brussels')).toBe(
      'Last saved on Test site B by Transfr Remote, 15 Oct 2026, 12:25',
    );
    expect(describeRemoteChange({ by: 'Transfr Admin', at, what: 'PublishVariant' }, 'Test site B', 'UTC')).toBe(
      'Last published on Test site B by Transfr Admin, 15 Oct 2026, 10:25',
    );
  });

  it('leaves out a user it has no name for, and shows an unknown change as named', () => {
    expect(describeRemoteChange({ by: null, at, what: 'Copy' }, 'Test site B', 'UTC')).toBe(
      'Last Copy on Test site B, 15 Oct 2026, 10:25',
    );
  });
});

describe('describeLastCopy', () => {
  const at = '2026-10-15T10:52:47.6602278+00:00';

  it('names the remote, the user here and the time in the given zone', () => {
    expect(describeLastCopy({ by: 'Transfr Admin', at }, 'Test site B', 'Europe/Brussels')).toBe(
      'Last copied to Test site B by Transfr Admin, 15 Oct 2026, 12:52',
    );
  });

  it('leaves out a user that no longer exists', () => {
    expect(describeLastCopy({ by: null, at }, 'Test site B', 'UTC')).toBe(
      'Last copied to Test site B, 15 Oct 2026, 10:52',
    );
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
    [note('NotOnTarget'), "It isn't on B: fields can only be copied to a node that is there."],
    [
      note('TargetHasDraft', { culture: 'nl-BE' }),
      "nl-BE has unpublished changes on B: the fields are saved there, not published, so nobody's draft goes live.",
    ],
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
