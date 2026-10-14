/**
 * The title and detail of the ProblemDetails in an error returned by tryExecute, or the fallback when there are none
 * (401, 403 and 404 carry no body).
 */
export function problemMessage(error: unknown, fallback: string): string {
  const problem = (error as { problemDetails?: { title?: string; detail?: string } } | undefined)?.problemDetails;
  return [problem?.title, problem?.detail].filter(Boolean).join(': ') || fallback;
}
