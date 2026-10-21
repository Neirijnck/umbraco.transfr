import { describe, expect, it } from 'vitest';
import { countByKind, describeItems } from './item-count.js';

describe('describeItems', () => {
  it.each([
    [0, 0, '0 nodes'],
    [1, 0, '1 node'],
    [2, 0, '2 nodes'],
    [0, 1, '1 media item'],
    [0, 3, '3 media items'],
    [2, 1, '2 nodes and 1 media item'],
  ])('describes %i nodes and %i media as %s', (nodes, media, expected) => {
    expect(describeItems(nodes, media)).toBe(expected);
  });
});

describe('countByKind', () => {
  it('counts media apart and everything else as nodes', () => {
    expect(countByKind([{ kind: 'Document' }, { kind: 'Media' }, { kind: 'Media' }, { kind: 'Document' }])).toEqual({
      nodes: 2,
      media: 2,
    });
    expect(countByKind([])).toEqual({ nodes: 0, media: 0 });
  });
});
