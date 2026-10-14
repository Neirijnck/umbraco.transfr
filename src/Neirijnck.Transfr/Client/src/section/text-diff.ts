export type DiffSegment = { text: string; kind: 'same' | 'removed' | 'added' };

/** Both sides of a text diff: the local text as same + removed parts, the remote text as same + added parts. */
export type TextDiff = { local: Array<DiffSegment>; remote: Array<DiffSegment> };

// Words, runs of whitespace, and single other characters, so punctuation and markup tags diff finely.
const TOKEN = /\s+|[\p{L}\p{N}_]+|[^\s\p{L}\p{N}_]/gu;

function tokenize(text: string): Array<string> {
  return text.match(TOKEN) ?? [];
}

function push(segments: Array<DiffSegment>, text: string, kind: DiffSegment['kind']) {
  if (!text) return;
  const last = segments[segments.length - 1];
  if (last?.kind === kind) last.text += text;
  else segments.push({ text, kind });
}

/**
 * Which words were removed from the local text and added in the remote one: a longest common subsequence over the
 * tokens, after the common start and end are set aside. Past `maxCells` (tokens × tokens of the differing middle) the
 * middle is shown as replaced as a whole, so a huge rich text can't freeze the view.
 */
export function diffWords(local: string, remote: string, maxCells = 250_000): TextDiff {
  const a = tokenize(local);
  const b = tokenize(remote);

  let start = 0;
  while (start < a.length && start < b.length && a[start] === b[start]) start++;
  let endA = a.length;
  let endB = b.length;
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) {
    endA--;
    endB--;
  }

  const result: TextDiff = { local: [], remote: [] };
  const head = a.slice(0, start).join('');
  push(result.local, head, 'same');
  push(result.remote, head, 'same');

  const middleA = a.slice(start, endA);
  const middleB = b.slice(start, endB);
  if (middleA.length * middleB.length > maxCells) {
    push(result.local, middleA.join(''), 'removed');
    push(result.remote, middleB.join(''), 'added');
  } else {
    // lengths[i][j]: the LCS length of middleA from i and middleB from j.
    const lengths = Array.from({ length: middleA.length + 1 }, () => new Array<number>(middleB.length + 1).fill(0));
    for (let i = middleA.length - 1; i >= 0; i--) {
      for (let j = middleB.length - 1; j >= 0; j--) {
        lengths[i]![j] =
          middleA[i] === middleB[j] ? lengths[i + 1]![j + 1]! + 1 : Math.max(lengths[i + 1]![j]!, lengths[i]![j + 1]!);
      }
    }

    let i = 0;
    let j = 0;
    while (i < middleA.length && j < middleB.length) {
      if (middleA[i] === middleB[j]) {
        push(result.local, middleA[i]!, 'same');
        push(result.remote, middleB[j]!, 'same');
        i++;
        j++;
      } else if (lengths[i + 1]![j]! >= lengths[i]![j + 1]!) {
        push(result.local, middleA[i++]!, 'removed');
      } else {
        push(result.remote, middleB[j++]!, 'added');
      }
    }
    for (; i < middleA.length; i++) push(result.local, middleA[i]!, 'removed');
    for (; j < middleB.length; j++) push(result.remote, middleB[j]!, 'added');
  }

  const tail = a.slice(endA).join('');
  push(result.local, tail, 'same');
  push(result.remote, tail, 'same');
  return result;
}
