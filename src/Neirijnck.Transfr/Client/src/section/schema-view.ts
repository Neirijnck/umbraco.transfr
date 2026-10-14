import type { SchemaProblemResponseModel, SeparateInstallResponseModel, TreeRowResponseModel } from '../api/index.js';

/** Why a row can't be copied: a short reason for the chip, every detail for the tooltip. */
export type RowBlock = { reason: string; details: Array<string>; inherited: boolean };

/** One schema problem in words, e.g. 'Document type "News Page" doesn't exist on Test site B'. */
export function describeSchemaProblem(problem: SchemaProblemResponseModel, remoteName: string): string {
  const what = problem.kind === 'DataType' ? 'Data type' : problem.isElement ? 'Element type' : 'Document type';
  const name = problem.name ? `"${problem.name}"` : problem.key;
  switch (problem.reason) {
    case 'MissingOnRemote':
      return `${what} ${name} doesn't exist on ${remoteName}`;
    case 'MissingLocally':
      return `${what} ${name} doesn't exist here`;
    case 'Differs':
      return `${what} ${name} differs on ${remoteName}`;
    default:
      return `${what} ${name}: ${problem.reason}`;
  }
}

/**
 * Why this row can't be copied, or undefined when it can: its own schema problems, or a parent that can't be copied
 * and isn't on the other side (so the node would have nowhere to go). `blockedParent` is that parent's name.
 */
export function blockOf(
  row: TreeRowResponseModel,
  blockedParent: string | undefined,
  remoteName: string,
): RowBlock | undefined {
  const problems = row.schemaProblems ?? [];
  if (problems.length > 0) {
    return {
      reason: describeSchemaProblem(problems[0]!, remoteName),
      details: problems.flatMap((problem) => [
        describeSchemaProblem(problem, remoteName),
        ...problem.details.map((detail) => `  ${detail}`),
      ]),
      inherited: false,
    };
  }

  if (blockedParent !== undefined && isOnOneSideOnly(row)) {
    const reason = `Its parent "${blockedParent}" can't be copied`;
    return { reason, details: [reason], inherited: true };
  }

  return undefined;
}

/**
 * Whether a blocked row passes its block on to its children: only when it isn't on the other side, so its children
 * have no parent to go under there.
 */
export function passesBlockOn(row: TreeRowResponseModel): boolean {
  return isOnOneSideOnly(row);
}

function isOnOneSideOnly(row: TreeRowResponseModel): boolean {
  return row.status === 'OnlyLocal' || row.status === 'OnlyRemote';
}

/** The banner text for a remote that was installed separately, naming at most two types as evidence. */
export function describeSeparateInstall(evidence: Array<SeparateInstallResponseModel>, remoteName: string): string {
  const named = evidence.slice(0, 2).map((item) => `"${item.name}" (${item.alias})`);
  const more = evidence.length > 2 ? ` and ${evidence.length - 2} more` : '';
  const [types, have] = evidence.length === 1 ? ['document type', 'has'] : ['document types', 'have'];
  return (
    `${remoteName} looks like a separate installation: the ${types} ${named.join(' and ')}${more} ` +
    `${have} the same alias there but another key. Nodes can't be matched by key, so nothing can be copied to or from it.`
  );
}
