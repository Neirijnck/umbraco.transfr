/**
 * How one side of a compared row is drawn:
 * - same: the node is here and on the other side under the same parent;
 * - only: the node is only on this side;
 * - missing: the node is only on the other side (drawn as a placeholder);
 * - moved: the node is here, and on the other side under another parent;
 * - elsewhere: the node is under another parent on this side (drawn as a placeholder that points there).
 */
export type SideKind = 'same' | 'only' | 'missing' | 'moved' | 'elsewhere';

export type RowView = { local: SideKind; remote: SideKind; known: boolean };

/**
 * Maps a row status from the compare endpoint to how each side is drawn. An unknown status (a newer server) draws both
 * sides plainly and is flagged, so it is never shown as identical by accident.
 */
export function rowView(status: string): RowView {
  switch (status) {
    case 'InBoth':
      return { local: 'same', remote: 'same', known: true };
    case 'OnlyLocal':
      return { local: 'only', remote: 'missing', known: true };
    case 'OnlyRemote':
      return { local: 'missing', remote: 'only', known: true };
    case 'ElsewhereOnRemote':
      return { local: 'moved', remote: 'elsewhere', known: true };
    case 'ElsewhereLocally':
      return { local: 'elsewhere', remote: 'moved', known: true };
    default:
      return { local: 'same', remote: 'same', known: false };
  }
}
