import { describe, expect, it } from 'vitest';
import { diffWords } from './text-diff.js';

const join = (segments: Array<{ text: string }>) => segments.map((segment) => segment.text).join('');

describe('diffWords', () => {
  it('keeps both texts whole on each side', () => {
    const local = 'About the <b>team</b>, since 2020.';
    const remote = 'About our <b>people</b>, since 2021!';
    const diff = diffWords(local, remote);

    expect(join(diff.local)).toBe(local);
    expect(join(diff.remote)).toBe(remote);
  });

  it('gives one same segment for equal texts', () => {
    expect(diffWords('About', 'About')).toEqual({
      local: [{ text: 'About', kind: 'same' }],
      remote: [{ text: 'About', kind: 'same' }],
    });
  });

  it('marks the changed word only', () => {
    expect(diffWords('First block', 'First item')).toEqual({
      local: [
        { text: 'First ', kind: 'same' },
        { text: 'block', kind: 'removed' },
      ],
      remote: [
        { text: 'First ', kind: 'same' },
        { text: 'item', kind: 'added' },
      ],
    });
  });

  it('keeps an equal word between two changes', () => {
    expect(diffWords('a b c d', 'a x c y').local).toEqual([
      { text: 'a ', kind: 'same' },
      { text: 'b', kind: 'removed' },
      { text: ' c ', kind: 'same' },
      { text: 'd', kind: 'removed' },
    ]);
  });

  it('marks an insertion on the remote side only', () => {
    const diff = diffWords('About', 'About (edited on B)');

    expect(diff.local).toEqual([{ text: 'About', kind: 'same' }]);
    expect(diff.remote).toEqual([
      { text: 'About', kind: 'same' },
      { text: ' (edited on B)', kind: 'added' },
    ]);
  });

  it('treats markup characters as their own tokens', () => {
    const diff = diffWords('<p>Old.</p>', '<p>New.</p>');

    expect(diff.local).toEqual([
      { text: '<p>', kind: 'same' },
      { text: 'Old', kind: 'removed' },
      { text: '.</p>', kind: 'same' },
    ]);
  });

  it('handles an empty side', () => {
    expect(diffWords('', 'New')).toEqual({ local: [], remote: [{ text: 'New', kind: 'added' }] });
    expect(diffWords('Old', '')).toEqual({ local: [{ text: 'Old', kind: 'removed' }], remote: [] });
  });

  it('replaces the differing middle as a whole past the size limit, keeping the common start and end', () => {
    const diff = diffWords('start a b c end', 'start x y z end', 4);

    expect(diff.local).toEqual([
      { text: 'start ', kind: 'same' },
      { text: 'a b c', kind: 'removed' },
      { text: ' end', kind: 'same' },
    ]);
    expect(diff.remote[1]).toEqual({ text: 'x y z', kind: 'added' });
  });
});
