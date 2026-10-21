/** A number of nodes and media items in words, e.g. "2 nodes and 1 media item"; "0 nodes" when there are none. */
export function describeItems(nodes: number, media: number): string {
  const parts: Array<string> = [];
  if (nodes > 0 || media === 0) {
    parts.push(`${nodes} ${nodes === 1 ? 'node' : 'nodes'}`);
  }

  if (media > 0) {
    parts.push(`${media} ${media === 1 ? 'media item' : 'media items'}`);
  }

  return parts.join(' and ');
}

/** Nodes and media items among rows that carry a kind (Document or Media). */
export function countByKind(rows: ReadonlyArray<{ kind: string }>): { nodes: number; media: number } {
  const media = rows.filter((row) => row.kind === 'Media').length;
  return { nodes: rows.length - media, media };
}
