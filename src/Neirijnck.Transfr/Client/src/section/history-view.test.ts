import { describe, expect, it } from 'vitest';
import type { TransferHistoryDocumentResponseModel } from '../api/index.js';
import { describeTransferHeadline, outcomeWord, summarizeTransfer } from './history-view.js';

const document = (outcome: string, kind = 'Document'): TransferHistoryDocumentResponseModel => ({
  key: 'k',
  name: 'About',
  outcome,
  kind,
});

describe('describeTransferHeadline', () => {
  // As the history answers it (measured on test site A): UTC, Brussels was UTC+2 then.
  const at = '2026-10-15T10:25:28.2757756+00:00';

  it('names the moment in the given zone and the user', () => {
    expect(describeTransferHeadline({ at, by: 'Transfr Admin', documents: [] }, 'Europe/Brussels')).toBe(
      '15 Oct 2026, 12:25 by Transfr Admin',
    );
  });

  it('leaves out a user that no longer exists', () => {
    expect(describeTransferHeadline({ at, by: null, documents: [] }, 'UTC')).toBe('15 Oct 2026, 10:25');
  });
});

describe('summarizeTransfer', () => {
  it('counts created before updated, whatever the order', () => {
    expect(summarizeTransfer([document('Update'), document('Update'), document('Create')])).toBe(
      '3 nodes: 1 created, 2 updated',
    );
  });

  it('says node for one', () => {
    expect(summarizeTransfer([document('Update')])).toBe('1 node: 1 updated');
  });

  it('counts media items apart from nodes', () => {
    expect(summarizeTransfer([document('Update'), document('Create', 'Media'), document('Update', 'Media')])).toBe(
      '1 node and 2 media items: 1 created, 2 updated',
    );
  });

  it('shows an unknown outcome as named, after the known ones', () => {
    expect(summarizeTransfer([document('Moved'), document('Create')])).toBe('2 nodes: 1 created, 1 Moved');
  });
});

describe('outcomeWord', () => {
  it('names created and updated, and leaves the rest as they are', () => {
    expect(['Create', 'Update', 'Other'].map(outcomeWord)).toEqual(['created', 'updated', 'Other']);
  });
});
