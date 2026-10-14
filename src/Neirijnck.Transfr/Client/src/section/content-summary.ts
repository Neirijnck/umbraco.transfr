import type { ContentDifferenceResponseModel } from '../api/index.js';

/**
 * One difference in words: a property by its alias, the rest by what it is, with the culture (and segment) it applies
 * to. An unknown kind (a newer server) is shown as it comes.
 */
export function describeDifference(difference: ContentDifferenceResponseModel): string {
  const what =
    difference.kind === 'Value'
      ? (difference.alias ?? 'a value')
      : difference.kind === 'Name'
        ? 'name'
        : difference.kind === 'State'
          ? 'publish state'
          : difference.kind === 'Template'
            ? 'template'
            : difference.kind === 'DocumentType'
              ? 'document type'
              : difference.kind;
  const where = [difference.culture, difference.segment].filter(Boolean).join(', ');
  return where ? `${what} (${where})` : what;
}

/**
 * A short line for the tree ("title, blocks, publish state"), at most `max` items and then "+N more", without
 * repeating an item that differs in several cultures.
 */
export function summarizeDifferences(differences: Array<ContentDifferenceResponseModel>, max = 3): string {
  const items = [
    ...new Set(differences.map((difference) => describeDifference({ ...difference, culture: null, segment: null }))),
  ];
  const shown = items.slice(0, max).join(', ');
  return items.length > max ? `${shown}, +${items.length - max} more` : shown;
}
