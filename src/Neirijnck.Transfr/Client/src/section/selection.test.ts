import { describe, expect, it } from 'vitest';
import type { TreeRowResponseModel } from '../api/index.js';
import {
  EMPTY_BATCH,
  addToSelection,
  batchFromRequest,
  batchToRequest,
  coverForChildren,
  describeBatch,
  fromRequest,
  isEmptyBatch,
  isWhole,
  loadBatch,
  saveBatch,
  setFields,
  setIncludeDescendants,
  toRequest,
  toggleSelected,
  whyNotSelectable,
} from './selection.js';
import type { Batch, NodePick } from './selection.js';

const row = (status: string): TreeRowResponseModel => ({ key: 'r', status, orderDiffers: false });

const batch = (documents: Array<[string, NodePick]>, media: Array<[string, boolean]> = []): Batch => ({
  documents: new Map(documents),
  media: new Map(media),
});

describe('selection', () => {
  it('picks a node on its own and drops it again', () => {
    const picked = toggleSelected(new Map(), 'a');
    expect([...picked]).toEqual([['a', false]]);
    expect(toggleSelected(picked, 'a').size).toBe(0);
  });

  it('includes everything below a picked node only', () => {
    const picked = setIncludeDescendants(toggleSelected(new Map(), 'a'), 'a', true);
    expect(picked.get('a')).toBe(true);
    expect(setIncludeDescendants(picked, 'b', true).has('b')).toBe(false);
  });

  it('adds nodes without changing the choice for one already picked', () => {
    const picked = addToSelection(new Map([['a', true]]), ['a', 'b']);
    expect([...picked]).toEqual([
      ['a', true],
      ['b', false],
    ]);
  });

  it('covers the children of a node picked with everything below it, and their children', () => {
    const picked = new Map([
      ['home', true],
      ['news', false],
    ]);
    expect(coverForChildren(picked, 'home', 'Home', undefined)).toBe('Home');
    expect(coverForChildren(picked, 'news', 'News', undefined)).toBeUndefined();
    expect(coverForChildren(picked, 'about', 'About', 'Home')).toBe('Home');
    expect(coverForChildren(new Map(), 'home', 'Home', undefined)).toBeUndefined();
  });

  it('round-trips through the request, in the order picked', () => {
    const picked = new Map([
      ['b', true],
      ['a', false],
    ]);
    expect(toRequest(picked)).toEqual([
      { key: 'b', includeDescendants: true },
      { key: 'a', includeDescendants: false },
    ]);
    expect([...fromRequest(toRequest(picked))]).toEqual([...picked]);
  });

  const title = { kind: 'Value', alias: 'title', culture: null, segment: null };

  it('picks some fields of a node, and drops the node when none are left', () => {
    const picked = setFields(new Map(), 'a', [title]);
    expect([...picked]).toEqual([['a', [title]]]);
    expect(isWhole(picked.get('a'))).toBe(false);
    expect(setFields(picked, 'a', []).size).toBe(0);
  });

  it('lets the whole node win over its fields', () => {
    const whole = setFields(new Map([['a', true]]), 'a', [title]);
    expect(whole.get('a')).toBe(true);

    const byFields = setFields(new Map(), 'a', [title]);
    expect(toggleSelected(byFields, 'a').get('a')).toBe(false);
    expect(addToSelection(byFields, ['a']).get('a')).toBe(false);
    expect(setIncludeDescendants(byFields, 'a', true).get('a')).toEqual([title]);
    expect(coverForChildren(byFields, 'a', 'A', undefined)).toBeUndefined();
  });

  it('sends picked fields with the node, never with everything below it', () => {
    const picked = new Map<string, NodePick>([
      ['a', [title]],
      ['b', true],
    ]);
    expect(toRequest(picked)).toEqual([
      { key: 'a', includeDescendants: false, fields: [title] },
      { key: 'b', includeDescendants: true },
    ]);
    expect([...fromRequest(toRequest(picked))]).toEqual([...picked]);
    expect(fromRequest([{ key: 'c', includeDescendants: false, fields: [] }]).get('c')).toBe(false);
  });

  it.each([
    [batch([['a', false]]), '1 node selected'],
    [batch([['a', true]]), '1 node selected, 1 with everything below it'],
    [
      batch([
        ['a', true],
        ['b', [title]],
      ]),
      '2 nodes selected, 1 with everything below it, 1 with only some fields',
    ],
    [
      batch([
        ['a', true],
        ['b', true],
        ['c', false],
      ]),
      '3 nodes selected, 2 with everything below them',
    ],
    [batch([], [['m', false]]), '1 media item selected'],
    [
      batch(
        [['a', false]],
        [
          ['m', true],
          ['n', false],
        ],
      ),
      '1 node and 2 media items selected, 1 with everything below it',
    ],
  ])('describes %o', (selection, expected) => {
    expect(describeBatch(selection)).toBe(expected);
  });
});

describe('batch', () => {
  it('sends the nodes, then the media items with their kind, and reads them back apart', () => {
    const picked = batch(
      [['a', true]],
      [
        ['m', true],
        ['n', false],
      ],
    );
    expect(batchToRequest(picked)).toEqual([
      { key: 'a', includeDescendants: true },
      { key: 'm', includeDescendants: true, kind: 'Media' },
      { key: 'n', includeDescendants: false, kind: 'Media' },
    ]);
    const back = batchFromRequest(batchToRequest(picked));
    expect([...back.documents]).toEqual([['a', true]]);
    expect([...back.media]).toEqual([
      ['m', true],
      ['n', false],
    ]);
  });

  it('reads an entry without a kind, or kind Document, as a node', () => {
    const back = batchFromRequest([
      { key: 'a', includeDescendants: false },
      { key: 'b', includeDescendants: true, kind: 'Document' },
    ]);
    expect([...back.documents]).toEqual([
      ['a', false],
      ['b', true],
    ]);
    expect(back.media.size).toBe(0);
  });

  it('is empty only with no nodes and no media', () => {
    expect(isEmptyBatch(EMPTY_BATCH)).toBe(true);
    expect(isEmptyBatch(batch([], [['m', false]]))).toBe(false);
    expect(isEmptyBatch(batch([['a', false]]))).toBe(false);
  });
});

describe('batch storage', () => {
  const memory = () => {
    const items = new Map<string, string>();
    return {
      items,
      getItem: (key: string) => items.get(key) ?? null,
      setItem: (key: string, value: string) => void items.set(key, value),
      removeItem: (key: string) => void items.delete(key),
    };
  };

  it('keeps the batch per remote, nodes and media, and gives it back in the order picked', () => {
    const storage = memory();
    saveBatch(
      storage,
      'b',
      batch(
        [
          ['x', true],
          ['y', false],
        ],
        [['m', true]],
      ),
    );
    saveBatch(storage, 'c', batch([['z', false]]));

    expect([...loadBatch(storage, 'b').documents]).toEqual([
      ['x', true],
      ['y', false],
    ]);
    expect([...loadBatch(storage, 'b').media]).toEqual([['m', true]]);
    expect([...loadBatch(storage, 'c').documents]).toEqual([['z', false]]);
  });

  it('keeps picked fields, and drops a stored pick whose fields it can not read', () => {
    const storage = memory();
    const title = { kind: 'Value', alias: 'title', culture: null, segment: null };
    saveBatch(storage, 'b', batch([['x', [title]]]));
    expect([...loadBatch(storage, 'b').documents]).toEqual([['x', [title]]]);

    storage.items.set(
      'transfr.batch.b',
      JSON.stringify([
        { key: 'x', includeDescendants: false, fields: [{ alias: 'title' }] },
        { key: 'y', includeDescendants: false, fields: 'title' },
        { key: 'z', includeDescendants: false, fields: null },
        { key: 'f', includeDescendants: false, kind: 'Folder' },
        { key: 'g', includeDescendants: false, kind: 'Media', fields: [title] },
      ]),
    );
    expect([...loadBatch(storage, 'b').documents]).toEqual([['z', false]]);
    expect(loadBatch(storage, 'b').media.size).toBe(0);
  });

  it('removes an empty batch', () => {
    const storage = memory();
    saveBatch(storage, 'b', batch([], [['m', true]]));
    saveBatch(storage, 'b', EMPTY_BATCH);

    expect(storage.items.size).toBe(0);
  });

  it('gives an empty batch for nothing stored, something else stored, or storage that throws', () => {
    const storage = memory();
    storage.setItem('transfr.batch.b', '{"not":"a list"}');
    storage.setItem('transfr.batch.c', '[{"key":"x"},{"key":"y","includeDescendants":true}]');
    const broken = {
      getItem: () => {
        throw new Error('denied');
      },
      setItem: () => {
        throw new Error('full');
      },
      removeItem: () => undefined,
    };

    expect(isEmptyBatch(loadBatch(storage, 'b'))).toBe(true);
    expect([...loadBatch(storage, 'c').documents]).toEqual([['y', true]]);
    expect(isEmptyBatch(loadBatch(undefined, 'b'))).toBe(true);
    expect(isEmptyBatch(loadBatch(broken, 'b'))).toBe(true);
    expect(() => saveBatch(broken, 'b', batch([['x', false]]))).not.toThrow();
  });
});

describe('whyNotSelectable', () => {
  it.each(['InBoth', 'OnlyLocal', 'ElsewhereOnRemote'])('lets a node here in this place be picked (%s)', (status) => {
    expect(whyNotSelectable(row(status), undefined, false)).toBeUndefined();
  });

  it('refuses a node that is only on the remote or lives elsewhere here', () => {
    expect(whyNotSelectable(row('OnlyRemote'), undefined, false)).toBe('It is not here.');
    expect(whyNotSelectable(row('ElsewhereLocally'), undefined, false)).toMatch(/select it there/);
  });

  it('refuses a blocked node with the block reason, and everything on a refused remote', () => {
    const block = { reason: 'Document type "X" doesn\'t exist on B', details: [], inherited: false };
    expect(whyNotSelectable(row('OnlyLocal'), block, false)).toBe(block.reason);
    expect(whyNotSelectable(row('InBoth'), undefined, true)).toMatch(/installed separately/);
  });
});
