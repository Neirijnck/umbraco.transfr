import { describe, expect, it } from 'vitest';
import type { SchemaProblemResponseModel, TreeRowResponseModel } from '../api/index.js';
import { blockOf, describeSchemaProblem, describeSeparateInstall, passesBlockOn } from './schema-view.js';

const problem = (reason: string, extra: Partial<SchemaProblemResponseModel> = {}): SchemaProblemResponseModel => ({
  kind: 'DocumentType',
  key: 'k',
  name: 'News Page',
  isElement: false,
  reason,
  details: [],
  ...extra,
});

const row = (
  status: string,
  schemaProblems: Array<SchemaProblemResponseModel> | null = null,
): TreeRowResponseModel => ({
  key: 'r',
  status,
  orderDiffers: false,
  schemaProblems,
});

describe('describeSchemaProblem', () => {
  it.each([
    [problem('MissingOnRemote'), 'Document type "News Page" doesn\'t exist on B'],
    [problem('MissingLocally'), 'Document type "News Page" doesn\'t exist here'],
    [problem('Differs'), 'Document type "News Page" differs on B'],
    [problem('MissingOnRemote', { isElement: true, name: 'Text' }), 'Element type "Text" doesn\'t exist on B'],
    [problem('Differs', { kind: 'DataType', name: 'Blocks' }), 'Data type "Blocks" differs on B'],
    [problem('MissingOnRemote', { name: null }), "Document type k doesn't exist on B"],
  ])('describes %o', (input, expected) => {
    expect(describeSchemaProblem(input, 'B')).toBe(expected);
  });
});

describe('blockOf', () => {
  it('blocks a row on its own schema problems, with every detail for the tooltip', () => {
    const block = blockOf(
      row('InBoth', [problem('Differs', { details: ["property 'title' uses another data type"] })]),
      undefined,
      'B',
    );

    expect(block).toEqual({
      reason: 'Document type "News Page" differs on B',
      details: ['Document type "News Page" differs on B', "  property 'title' uses another data type"],
      inherited: false,
    });
  });

  it('passes a block to a child that would have nowhere to go', () => {
    expect(blockOf(row('OnlyLocal'), 'Landing', 'B')).toEqual({
      reason: 'Its parent "Landing" can\'t be copied',
      details: ['Its parent "Landing" can\'t be copied'],
      inherited: true,
    });
  });

  it('does not block a child that exists on both sides or elsewhere', () => {
    expect(blockOf(row('InBoth'), 'Landing', 'B')).toBeUndefined();
    expect(blockOf(row('ElsewhereOnRemote'), 'Landing', 'B')).toBeUndefined();
  });

  it('does not block a row without problems or a blocked parent', () => {
    expect(blockOf(row('OnlyLocal'), undefined, 'B')).toBeUndefined();
    expect(blockOf(row('OnlyLocal', []), undefined, 'B')).toBeUndefined();
  });
});

describe('passesBlockOn', () => {
  it('only from a node that is on one side only', () => {
    expect(passesBlockOn(row('OnlyLocal'))).toBe(true);
    expect(passesBlockOn(row('OnlyRemote'))).toBe(true);
    expect(passesBlockOn(row('InBoth'))).toBe(false);
    expect(passesBlockOn(row('ElsewhereOnRemote'))).toBe(false);
  });
});

describe('describeSeparateInstall', () => {
  const evidence = (name: string, alias: string) => ({ name, alias, localKey: 'l', remoteKey: 'r' });

  it('names the evidence', () => {
    expect(describeSeparateInstall([evidence('Test Page', 'testPage')], 'C')).toBe(
      'C looks like a separate installation: the document type "Test Page" (testPage) has the same alias there but ' +
        "another key. Nodes can't be matched by key, so nothing can be copied to or from it.",
    );
  });

  it('names at most two and counts the rest', () => {
    const text = describeSeparateInstall(
      [evidence('A', 'a'), evidence('B', 'b'), evidence('C', 'c'), evidence('D', 'd')],
      'Remote',
    );
    expect(text).toContain('the document types "A" (a) and "B" (b) and 2 more have');
  });
});
