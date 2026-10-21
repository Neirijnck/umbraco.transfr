import { describe, expect, it } from 'vitest';
import type { BlockDiffResponseModel, FieldDiffResponseModel } from '../api/index.js';
import { blockStatusLabel, describeState, fieldLabel, fieldValue, isImageUrl } from './diff-labels.js';

const field = (
  kind: string,
  local: string | null,
  remote: string | null,
  culture: string | null = null,
): FieldDiffResponseModel => ({
  kind,
  culture,
  segment: null,
  local,
  remote,
  differs: local !== remote,
});

const block = (status: string, localIndex: number | null, remoteIndex: number | null): BlockDiffResponseModel => ({
  key: 'k',
  status,
  localIndex,
  remoteIndex,
  values: [],
  settings: [],
});

describe('describeState', () => {
  it.each([
    ['Published', 'Published'],
    ['PublishedPendingChanges', 'Published, with unpublished changes'],
    ['Draft', 'Draft'],
    ['SomethingNew', 'SomethingNew'],
  ])('describes %s', (state, expected) => {
    expect(describeState(state)).toBe(expected);
  });

  it('gives nothing for no state', () => {
    expect(describeState(null)).toBeUndefined();
  });
});

describe('fieldLabel and fieldValue', () => {
  it('labels a field with its culture', () => {
    expect(fieldLabel(field('State', 'Draft', 'Published', 'nl-BE'))).toBe('Publish state (nl-BE)');
    expect(fieldLabel(field('DocumentType', 'a', 'a'))).toBe('Document type');
    expect(fieldLabel(field('MediaType', 'a', 'a'))).toBe('Media type');
  });

  it('shows states in words and other values as they are', () => {
    expect(fieldValue(field('State', 'PublishedPendingChanges', 'Draft'), 'local')).toBe(
      'Published, with unpublished changes',
    );
    expect(fieldValue(field('Name', 'About', 'About us'), 'remote')).toBe('About us');
    expect(fieldValue(field('Template', null, 'Home'), 'local')).toBeUndefined();
  });
});

describe('blockStatusLabel', () => {
  it('counts positions from 1', () => {
    expect(blockStatusLabel(block('Moved', 0, 2), 'B')).toBe('Moved, position 1 → 3');
  });

  it('mentions a new position only when a changed block also moved', () => {
    expect(blockStatusLabel(block('Changed', 1, 1), 'B')).toBe('Changed');
    expect(blockStatusLabel(block('Changed', 1, 0), 'B')).toBe('Changed, position 2 → 1');
  });

  it('names the side a block is on', () => {
    expect(blockStatusLabel(block('OnlyLocal', 0, null), 'Test site B')).toBe('Only here');
    expect(blockStatusLabel(block('OnlyRemote', null, 0), 'Test site B')).toBe('Only on Test site B');
  });
});

describe('isImageUrl', () => {
  it.each([
    ['/media/zn1jr2zj/red.png', true],
    ['https://localhost:44372/media/zn1jr2zj/Red.JPG', true],
    ['/media/abc/photo.webp?width=200', true],
    ['/media/abc/logo.svg#icon', true],
    ['/media/2fhdb0lj/probe.txt', false],
    ['/media/abc/report.pdf', false],
    ['/media/folder.png/file', false],
    ['', false],
    [null, false],
  ])('says %s is an image: %s', (url, expected) => {
    expect(isImageUrl(url)).toBe(expected);
  });
});
