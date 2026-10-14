import type { BlockDiffResponseModel, FieldDiffResponseModel } from '../api/index.js';

const STATES: Record<string, string> = {
  Published: 'Published',
  PublishedPendingChanges: 'Published, with unpublished changes',
  Draft: 'Draft',
  NotCreated: 'Not created',
  Trashed: 'In the recycle bin',
};

/** A publish state in words; an unknown state (a newer server) as it comes. */
export function describeState(state: string | null | undefined): string | undefined {
  return state ? (STATES[state] ?? state) : undefined;
}

/** The label of a document field row, with the culture (and segment) it applies to. */
export function fieldLabel(field: FieldDiffResponseModel): string {
  const what =
    field.kind === 'Name'
      ? 'Name'
      : field.kind === 'State'
        ? 'Publish state'
        : field.kind === 'Template'
          ? 'Template'
          : field.kind === 'DocumentType'
            ? 'Document type'
            : field.kind;
  const where = [field.culture, field.segment].filter(Boolean).join(', ');
  return where ? `${what} (${where})` : what;
}

/** A field's value for display: states in words, the rest as it comes. */
export function fieldValue(field: FieldDiffResponseModel, side: 'local' | 'remote'): string | undefined {
  const value = side === 'local' ? field.local : field.remote;
  return field.kind === 'State' ? describeState(value) : (value ?? undefined);
}

/** A block's status in words, its place on the page included where that matters (positions count from 1). */
export function blockStatusLabel(block: BlockDiffResponseModel, remoteName: string): string {
  const place = (index: number | null | undefined) => (index === null || index === undefined ? '?' : `${index + 1}`);
  switch (block.status) {
    case 'Same':
      return 'Same';
    case 'Changed':
      return block.localIndex === block.remoteIndex
        ? 'Changed'
        : `Changed, position ${place(block.localIndex)} → ${place(block.remoteIndex)}`;
    case 'Moved':
      return `Moved, position ${place(block.localIndex)} → ${place(block.remoteIndex)}`;
    case 'OnlyLocal':
      return 'Only here';
    case 'OnlyRemote':
      return `Only on ${remoteName}`;
    default:
      return block.status;
  }
}
