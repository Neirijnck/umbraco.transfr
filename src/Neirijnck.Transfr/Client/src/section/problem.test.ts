import { describe, expect, it } from 'vitest';
import { problemMessage } from './problem.js';

describe('problemMessage', () => {
  it('joins the title and detail', () => {
    const error = { problemDetails: { title: 'Reading the remote failed', detail: 'Connection refused' } };
    expect(problemMessage(error, 'Fallback')).toBe('Reading the remote failed: Connection refused');
  });

  it('uses the title alone when there is no detail', () => {
    expect(problemMessage({ problemDetails: { title: 'Signing in failed' } }, 'Fallback')).toBe('Signing in failed');
  });

  it.each([
    ['no error', undefined],
    ['an error without problem details (401, 403, 404)', {}],
    ['empty problem details', { problemDetails: {} }],
    ['an empty title and detail', { problemDetails: { title: '', detail: '' } }],
  ])('falls back for %s', (_case, error) => {
    expect(problemMessage(error, 'Fallback')).toBe('Fallback');
  });
});
