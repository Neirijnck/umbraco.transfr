import { describe, expect, it } from 'vitest';
import type { PlanRowResponseModel, TransferResponseModel, TransferRowResponseModel } from '../api/index.js';
import { countWrites, describeDrift, describeTransfer, resultLook, toDryRun, wroteAnything } from './transfer-view.js';

const row = (key: string, outcome: string, kind = 'Document'): PlanRowResponseModel => ({
  kind,
  key,
  parentKey: null,
  name: key,
  documentTypeName: null,
  outcome,
  notes: [],
  publishCultures: [],
  differences: [],
  schemaProblems: [],
  fingerprint: kind === 'Document' ? `${key}|absent` : null,
  changedOnRemote: null,
  lastCopied: null,
  fields: null,
});

const result = (key: string, outcome: string): TransferRowResponseModel => ({
  kind: 'Document',
  key,
  name: key,
  outcome: 'Create',
  result: outcome,
  publishCultures: [],
  error: null,
  targetState: null,
});

const response = (status: string, rows: Array<TransferRowResponseModel>, sortedParents = 0): TransferResponseModel => ({
  status,
  rows,
  drift: [],
  sortedParents,
  error: null,
  remoteRequests: 1,
});

describe('toDryRun', () => {
  it('sends back the document rows with outcome and fingerprint, not media', () => {
    expect(toDryRun([row('a', 'Create'), row('m', 'Create', 'Media')])).toEqual([
      { key: 'a', outcome: 'Create', fingerprint: 'a|absent' },
    ]);
  });
});

describe('countWrites', () => {
  it('counts creates, updates and only the conflicts chosen to overwrite', () => {
    const rows = [
      row('a', 'Create'),
      row('b', 'Update'),
      row('c', 'Conflict'),
      row('d', 'Conflict'),
      row('e', 'Unchanged'),
    ];
    expect(countWrites(rows, new Set(['c']))).toBe(3);
    expect(countWrites([row('m', 'Create', 'Media')], new Set())).toBe(0);
  });
});

describe('resultLook', () => {
  it('names every result and never shows an unknown one as harmless', () => {
    expect(['Written', 'Skipped', 'Failed', 'NotRun', 'MediaLater'].map((value) => resultLook(value).label)).toEqual([
      'Written',
      'Skipped: conflict',
      'Failed',
      'Not run',
      'Media: later',
    ]);
    expect(resultLook('Deleted')).toMatchObject({ label: 'Deleted', color: 'danger' });
  });
});

describe('describeDrift', () => {
  it.each([
    [
      { key: 'k', name: 'About', reason: 'Saved', was: 'Update', now: 'Update' },
      '"About" was saved here or on B since the dry run.',
    ],
    [
      { key: 'k', name: 'About', reason: 'OutcomeChanged', was: 'Update', now: 'Conflict' },
      '"About" would now be "Conflict" instead of "Update".',
    ],
    [
      { key: 'k', name: null, reason: 'NoLongerPlanned', was: 'Create', now: null },
      'A node from the dry run is no longer in the plan.',
    ],
  ])('describes %o', (drift, expected) => {
    expect(describeDrift(drift, 'B')).toBe(expected);
  });
});

describe('describeTransfer', () => {
  it('counts what was written and sorted', () => {
    expect(describeTransfer(response('Completed', [result('a', 'Written'), result('b', 'Unchanged')], 1), 'B')).toBe(
      'Copied 1 node to B, and put in order under 1 parent.',
    );
  });

  it('says that what was written before a failure stays written', () => {
    expect(
      describeTransfer(
        response('Failed', [result('a', 'Written'), result('b', 'Written'), result('c', 'Failed')]),
        'B',
      ),
    ).toBe('Stopped at a failure. 2 nodes were copied before it and stay copied.');
    expect(describeTransfer(response('Failed', [result('a', 'Written'), result('b', 'Failed')]), 'B')).toBe(
      'Stopped at a failure. 1 node was copied before it and stays copied.',
    );
    expect(describeTransfer(response('Failed', [result('a', 'Failed')]), 'B')).toBe(
      'Stopped at a failure. Nothing was copied.',
    );
  });

  it('says nothing was copied when refused', () => {
    expect(describeTransfer(response('Refused', []), 'B')).toMatch(/^Nothing was copied/);
  });
});

describe('wroteAnything', () => {
  it('is true after a write or a failure (the remote may have saved it), not otherwise', () => {
    expect(wroteAnything(response('Completed', [result('a', 'Written')]))).toBe(true);
    expect(wroteAnything(response('Failed', [result('a', 'Failed')]))).toBe(true);
    expect(wroteAnything(response('Completed', [result('a', 'Unchanged')]))).toBe(false);
  });
});
