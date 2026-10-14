import { describe, expect, it } from 'vitest';
import type { ContentDifferenceResponseModel } from '../api/index.js';
import { describeDifference, summarizeDifferences } from './content-summary.js';

const difference = (
  kind: string,
  alias?: string,
  culture?: string,
  segment?: string,
): ContentDifferenceResponseModel => ({
  kind,
  alias: alias ?? null,
  culture: culture ?? null,
  segment: segment ?? null,
});

describe('describeDifference', () => {
  it.each([
    [difference('Value', 'title'), 'title'],
    [difference('Value', 'body', 'nl-BE'), 'body (nl-BE)'],
    [difference('Value', 'body', 'nl-BE', 'mobile'), 'body (nl-BE, mobile)'],
    [difference('Name', undefined, 'nl-BE'), 'name (nl-BE)'],
    [difference('State'), 'publish state'],
    [difference('Template'), 'template'],
    [difference('DocumentType'), 'document type'],
    [difference('Value'), 'a value'],
    [difference('SomethingNew'), 'SomethingNew'],
  ])('describes %o as %s', (input, expected) => {
    expect(describeDifference(input)).toBe(expected);
  });
});

describe('summarizeDifferences', () => {
  it('lists the differences in the order they come', () => {
    expect(summarizeDifferences([difference('State'), difference('Value', 'title')])).toBe('publish state, title');
  });

  it('names an item that differs in several cultures once', () => {
    expect(summarizeDifferences([difference('Value', 'body', 'en-US'), difference('Value', 'body', 'nl-BE')])).toBe(
      'body',
    );
  });

  it('shows at most three and counts the rest', () => {
    const many = ['a', 'b', 'c', 'd', 'e'].map((alias) => difference('Value', alias));
    expect(summarizeDifferences(many)).toBe('a, b, c, +2 more');
  });

  it('shows exactly three without a count', () => {
    const three = ['a', 'b', 'c'].map((alias) => difference('Value', alias));
    expect(summarizeDifferences(three)).toBe('a, b, c');
  });

  it('gives an empty line for no differences', () => {
    expect(summarizeDifferences([])).toBe('');
  });
});
