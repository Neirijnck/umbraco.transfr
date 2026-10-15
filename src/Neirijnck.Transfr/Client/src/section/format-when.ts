/**
 * A moment as the views show it, e.g. "15 Oct 2026, 12:52": en-GB, in the viewer's zone unless one is named.
 */
export function formatWhen(at: string, timeZone?: string): string {
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(at));
}
