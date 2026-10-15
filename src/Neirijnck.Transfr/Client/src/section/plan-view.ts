import type {
  LastCopyResponseModel,
  PlanNoteResponseModel,
  PlanRowResponseModel,
  RemoteChangeResponseModel,
} from '../api/index.js';

export type OutcomeLook = {
  label: string;
  color: 'default' | 'positive' | 'warning' | 'danger';
  look: 'primary' | 'outline';
  icon: string;
};

/** How an outcome is shown. An unknown outcome (a newer server) is shown as it comes, never as harmless. */
export function outcomeLook(outcome: string): OutcomeLook {
  switch (outcome) {
    case 'Create':
      return { label: 'Create', color: 'positive', look: 'primary', icon: 'icon-add' };
    case 'Update':
      return { label: 'Update', color: 'default', look: 'primary', icon: 'icon-edit' };
    case 'Unchanged':
      return { label: 'Unchanged', color: 'default', look: 'outline', icon: 'icon-check' };
    case 'Conflict':
      return { label: 'Conflict: skipped', color: 'warning', look: 'primary', icon: 'icon-alert' };
    case 'Blocked':
      return { label: 'Blocked', color: 'danger', look: 'outline', icon: 'icon-block' };
    default:
      return { label: outcome, color: 'danger', look: 'outline', icon: 'icon-help-alt' };
  }
}

/** The outcomes in the order the summary lists them. */
export const OUTCOMES = ['Create', 'Update', 'Conflict', 'Blocked', 'Unchanged'] as const;

/** Rows per outcome, outcomes that don't occur left out. */
export function countOutcomes(rows: ReadonlyArray<PlanRowResponseModel>): Map<string, number> {
  const counts = new Map<string, number>();
  for (const row of rows) {
    counts.set(row.outcome, (counts.get(row.outcome) ?? 0) + 1);
  }
  return counts;
}

/**
 * What happens to the publish state of a row that is written: published (in which cultures) or saved only. Undefined
 * for a row that isn't written. The API lists null for invariant content (its type says string: OpenAPI drops the
 * nullable item).
 */
export function describePublish(row: PlanRowResponseModel): string | undefined {
  if (row.kind !== 'Document' || !['Create', 'Update', 'Conflict'].includes(row.outcome)) {
    return undefined;
  }

  const cultures = row.publishCultures as ReadonlyArray<string | null>;
  if (cultures.length === 0) {
    return row.outcome === 'Create' ? 'Created as a draft, not published' : 'Saved, not published';
  }

  const named = cultures.filter((culture): culture is string => culture !== null);
  return named.length === 0 ? 'Published' : `Published in ${named.join(', ')}`;
}

// Audit log types (Umbraco's AuditType) as a verb; any other type is shown as the remote names it.
const CHANGE_VERBS: Readonly<Record<string, string>> = {
  New: 'created',
  Save: 'saved',
  SaveVariant: 'saved',
  Publish: 'published',
  PublishVariant: 'published',
  Unpublish: 'unpublished',
  UnpublishVariant: 'unpublished',
  RollBack: 'rolled back',
};

/**
 * Who last changed a conflicting document on the remote, e.g. "Last saved on Test site B by Transfr Admin, 15 Oct
 * 2026, 12:25". The time is shown in the viewer's zone unless one is named.
 */
export function describeRemoteChange(change: RemoteChangeResponseModel, remoteName: string, timeZone?: string): string {
  const verb = CHANGE_VERBS[change.what] ?? change.what;
  return `Last ${verb} on ${remoteName}${change.by ? ` by ${change.by}` : ''}, ${formatWhen(change.at, timeZone)}`;
}

/**
 * The latest copy of a document from here to the remote, e.g. "Last copied to Test site B by Transfr Admin, 15 Oct
 * 2026, 12:52".
 */
export function describeLastCopy(copy: LastCopyResponseModel, remoteName: string, timeZone?: string): string {
  return `Last copied to ${remoteName}${copy.by ? ` by ${copy.by}` : ''}, ${formatWhen(copy.at, timeZone)}`;
}

function formatWhen(at: string, timeZone: string | undefined): string {
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(at));
}

/** One note in words, naming the remote, the item and the property it is about. */
export function describeNote(note: PlanNoteResponseModel, remoteName: string): string {
  const item = note.name ? `"${note.name}"` : 'an item';
  const property = note.propertyName ?? note.propertyAlias;
  const inProperty = property ? `"${property}"${note.culture ? ` (${note.culture})` : ''}` : 'Its content';
  const culture = note.culture ?? 'The content';
  switch (note.kind) {
    case 'SchemaMismatch':
      return `The schema behind ${item} doesn't match on ${remoteName}.`;
    case 'ParentMissingOnTarget':
      return `Its parent ${item} isn't on ${remoteName}.`;
    case 'ParentBlocked':
      return `Its parent ${item} can't be copied.`;
    case 'MissingReference':
      return `${inProperty} links to ${item}, which isn't on ${remoteName}.`;
    case 'MemberReference':
      return `${inProperty} references a member; members are never copied.`;
    case 'SameNameOtherKey':
      return `${remoteName} has another node called ${item} under the same parent.`;
    case 'TrashedOnTarget':
      return `It is in the recycle bin on ${remoteName}.`;
    case 'DocumentTypeDiffers':
      return `It has another document type on ${remoteName}${note.name ? ` (${item})` : ''}.`;
    case 'NoBaseline':
      return `It differs on ${remoteName}, and there is no earlier copy from here to tell who changed it.`;
    case 'ChangedSinceLastCopy':
      return `It changed on ${remoteName} since the last copy from here.`;
    case 'DraftOnly':
      return `${culture} has unpublished changes here: the draft is copied, not published.`;
    case 'PublishStateNotApplied':
      return `${culture} is published on ${remoteName} but not here: it stays published, a copy never unpublishes.`;
    case 'ParentNotPublished':
      return `Its parent ${item} isn't published on ${remoteName}: it is saved as a draft, not published.`;
    case 'PlacedAfter':
      return note.key
        ? `It takes its place here among its siblings on ${remoteName}: right after ${item}.`
        : `It takes its place here among its siblings on ${remoteName}: first.`;
    case 'UnderAnotherParentOnTarget':
      return `It lives under ${item} on ${remoteName}: updated there, not moved.`;
    case 'MediaMissingHere':
      return `${inProperty} uses a media item that doesn't exist here either: the reference is copied as it is.`;
    default:
      return note.kind;
  }
}
