import { describe, expect, it } from 'vitest';
import type { SubtreeLevelResponseModel, TreeRowResponseModel } from '../api/index.js';
import { countSubtree, describeCounts, formatDuration, hasDifference, positionsToReveal } from './subtree.js';

const side = { name: 'n', hasChildren: false, documentTypeIcon: null, documentTypeName: null };

function row(key: string, status = 'InBoth', extra: Partial<TreeRowResponseModel> = {}): TreeRowResponseModel {
  return {
    key,
    status,
    local: status === 'OnlyRemote' || status === 'ElsewhereLocally' ? null : side,
    remote: status === 'OnlyLocal' || status === 'ElsewhereOnRemote' ? null : side,
    otherParentKey: null,
    sameNameKey: null,
    orderDiffers: false,
    content: status === 'InBoth' ? { status: 'Identical', differences: [], error: null } : null,
    schemaProblems: null,
    ...extra,
  };
}

const changed = {
  content: {
    status: 'Different',
    differences: [{ kind: 'Value', alias: 'title', culture: null, segment: null }],
    error: null,
  },
};

// root > home > (about, news > (article*, moved*), moved*), with * = a difference.
const levels: Array<SubtreeLevelResponseModel> = [
  { parentKey: null, rows: [row('home')] },
  {
    parentKey: 'home',
    rows: [row('about'), row('news'), row('moved', 'ElsewhereOnRemote', { otherParentKey: 'news' })],
  },
  {
    parentKey: 'news',
    rows: [row('article', 'InBoth', changed), row('moved', 'ElsewhereLocally', { otherParentKey: 'home' })],
  },
];

describe('hasDifference', () => {
  it('is false only for an identical node in the same place', () => {
    expect(hasDifference(row('a'))).toBe(false);
    expect(hasDifference(row('a', 'InBoth', changed))).toBe(true);
    expect(hasDifference(row('a', 'InBoth', { orderDiffers: true }))).toBe(true);
    expect(
      hasDifference(
        row('a', 'InBoth', { content: { status: 'Failed', differences: [], error: 'Reading the remote failed' } }),
      ),
    ).toBe(true);
    expect(hasDifference(row('a', 'OnlyLocal'))).toBe(true);
    expect(hasDifference(row('a', 'OnlyLocal', { sameNameKey: 'b' }))).toBe(true);
  });
});

describe('countSubtree', () => {
  it('counts every node once, a moved node included', () => {
    expect(countSubtree(levels)).toEqual({
      nodes: 5,
      identical: 3,
      changed: 1,
      orderDiffers: 0,
      onlyLocal: 0,
      onlyRemote: 0,
      elsewhere: 1,
      failed: 0,
    });
  });

  it('counts a changed node that is also in another order in both groups, and not as identical', () => {
    const counts = countSubtree([{ parentKey: null, rows: [row('a', 'InBoth', { ...changed, orderDiffers: true })] }]);
    expect(counts).toMatchObject({ nodes: 1, identical: 0, changed: 1, orderDiffers: 1 });
  });
});

describe('positionsToReveal', () => {
  it('opens the path to every level that holds a difference', () => {
    expect([...positionsToReveal(levels)].sort()).toEqual(['/home', 'home/news']);
  });

  it('opens nothing at or above where the subtree started', () => {
    // Started at home: news is opened, home's own row is the caller's.
    expect([...positionsToReveal(levels.slice(1))]).toEqual(['home/news']);
  });

  it('opens nothing when everything is identical', () => {
    const identical = [
      { parentKey: null, rows: [row('home')] },
      { parentKey: 'home', rows: [row('about')] },
    ];
    expect(positionsToReveal(identical).size).toBe(0);
  });
});

describe('formatDuration', () => {
  it.each([
    [15.4, '15 ms'],
    [999.4, '999 ms'],
    [1000, '1 s'],
    [1240, '1.2 s'],
    [65000, '65 s'],
  ])('formats %d ms as %s', (milliseconds, expected) => {
    expect(formatDuration(milliseconds)).toBe(expected);
  });
});

describe('describeCounts', () => {
  it('says only what occurred', () => {
    expect(describeCounts(countSubtree(levels), 'Test site B')).toBe('5 nodes: 1 changed, 1 under another parent.');
  });

  it('says when everything is identical', () => {
    expect(describeCounts(countSubtree([{ parentKey: null, rows: [row('a')] }]), 'B')).toBe('1 node, all identical.');
  });

  it('names the remote for nodes only there', () => {
    expect(describeCounts(countSubtree([{ parentKey: null, rows: [row('a', 'OnlyRemote')] }]), 'Test site B')).toBe(
      '1 node: 1 only on Test site B.',
    );
  });
});
