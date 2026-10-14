import { describe, expect, it } from 'vitest';
import { rowView } from './tree-row-view.js';

describe('rowView', () => {
  it.each([
    ['InBoth', { local: 'same', remote: 'same', known: true }],
    ['OnlyLocal', { local: 'only', remote: 'missing', known: true }],
    ['OnlyRemote', { local: 'missing', remote: 'only', known: true }],
    ['ElsewhereOnRemote', { local: 'moved', remote: 'elsewhere', known: true }],
    ['ElsewhereLocally', { local: 'elsewhere', remote: 'moved', known: true }],
  ])('draws %s', (status, expected) => {
    expect(rowView(status)).toEqual(expected);
  });

  it('flags a status it does not know instead of drawing it as identical without a flag', () => {
    expect(rowView('SomethingNew')).toEqual({ local: 'same', remote: 'same', known: false });
  });

  it('matches statuses exactly, as the server sends them', () => {
    expect(rowView('inboth').known).toBe(false);
  });
});
