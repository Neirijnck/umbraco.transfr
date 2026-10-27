/** Which way a copy goes, as the API names it: from here to the remote, or from the remote to here. */
export type Direction = 'ToRemote' | 'FromRemote';

/** How the views name the two sides of a copy in one direction. */
export type Sides = {
  /** "to Test site B" or "from Test site B": the copy's way, in a headline or a button. */
  readonly way: string;
  /** Where the copy writes, as a place: "on Test site B" or "here". */
  readonly onTarget: string;
  /** Where the copy reads, as a place: "here" or "on Test site B". */
  readonly onSource: string;
  /** Where the copy writes, as the subject of a sentence: "Test site B" or "This environment". */
  readonly target: string;
  /** The start of a line with an item's state there after a transfer: "On Test site B now" or "Here now". */
  readonly targetNow: string;
  /** The choice to overwrite a conflict, in words. */
  readonly overwrite: string;
};

export function sidesOf(direction: Direction, remoteName: string): Sides {
  return direction === 'FromRemote'
    ? {
        way: `from ${remoteName}`,
        onTarget: 'here',
        onSource: `on ${remoteName}`,
        target: 'This environment',
        targetNow: 'Here now',
        overwrite: `Overwrite here: replace its version here with the one on ${remoteName}`,
      }
    : {
        way: `to ${remoteName}`,
        onTarget: `on ${remoteName}`,
        onSource: 'here',
        target: remoteName,
        targetNow: `On ${remoteName} now`,
        overwrite: `Overwrite on ${remoteName}: replace its version there with this one`,
      };
}
