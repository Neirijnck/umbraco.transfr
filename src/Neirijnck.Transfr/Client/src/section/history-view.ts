import type { TransferHistoryDocumentResponseModel, TransferHistoryItemResponseModel } from '../api/index.js';
import { formatWhen } from './format-when.js';

// What a transfer did to a document, as the history records it; any other outcome is shown as named.
const OUTCOME_WORDS: Readonly<Record<string, string>> = {
  Create: 'created',
  Update: 'updated',
};

/** When and by whom a transfer ran, e.g. "15 Oct 2026, 12:25 by Transfr Admin". */
export function describeTransferHeadline(transfer: TransferHistoryItemResponseModel, timeZone?: string): string {
  return `${formatWhen(transfer.at, timeZone)}${transfer.by ? ` by ${transfer.by}` : ''}`;
}

/** What a transfer wrote, e.g. "3 nodes: 1 created, 2 updated". */
export function summarizeTransfer(documents: ReadonlyArray<TransferHistoryDocumentResponseModel>): string {
  const counts = new Map<string, number>();
  for (const document of documents) {
    const word = outcomeWord(document.outcome);
    counts.set(word, (counts.get(word) ?? 0) + 1);
  }

  // Created before updated, then the rest as they came.
  const order = [...Object.values(OUTCOME_WORDS), ...counts.keys()];
  const parts = [...new Set(order)].filter((word) => counts.has(word)).map((word) => `${counts.get(word)} ${word}`);
  return `${documents.length} ${documents.length === 1 ? 'node' : 'nodes'}: ${parts.join(', ')}`;
}

/** A document's outcome as a word: "created", "updated", or the outcome as named. */
export function outcomeWord(outcome: string): string {
  return OUTCOME_WORDS[outcome] ?? outcome;
}
