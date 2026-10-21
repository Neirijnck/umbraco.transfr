import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUISelectElement } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type {
  RemoteResponseModel,
  TransferHistoryDocumentResponseModel,
  TransferHistoryItemResponseModel,
} from '../api/index.js';
import { brandStyles } from './brand.js';
import { describeTransferHeadline, outcomeWord, summarizeTransfer } from './history-view.js';
import { TRANSFR_NODE_DIFF_MODAL } from './node-diff-modal.token.js';
import { problemMessage } from './problem.js';

// Transfers per page; the endpoint allows up to 100.
const PAGE_SIZE = 50;

type History =
  | { state: 'loading' }
  | { state: 'failed'; message: string }
  | {
      state: 'loaded';
      transfers: Array<TransferHistoryItemResponseModel>;
      nextBefore?: number | null;
      // The next page: being read, or why reading it failed.
      more?: { state: 'loading' } | { state: 'failed'; message: string };
    };

/**
 * History view: the transfers from here to a remote, newest first, as the copy history recorded them (the nodes each
 * one wrote). A node that is still here opens its diff with the remote.
 */
@customElement('transfr-history')
export class TransfrHistoryElement extends UmbLitElement {
  @state()
  private _remotes?: Array<RemoteResponseModel>;

  @state()
  private _alias?: string;

  @state()
  private _history: History = { state: 'loading' };

  override connectedCallback() {
    super.connectedCallback();
    void this.#loadRemotes();
  }

  async #loadRemotes() {
    const { data } = await tryExecute(this, TransfrService.getRemotes());
    this._remotes = (data?.remotes ?? []).filter((remote) => remote.isValid);
    void this.#selectRemote(this._remotes[0]?.alias);
  }

  async #selectRemote(alias: string | undefined) {
    this._alias = alias;
    if (!alias) {
      return;
    }

    this._history = { state: 'loading' };
    const { data, error } = await tryExecute(
      this,
      TransfrService.getHistory({ path: { alias }, query: { take: PAGE_SIZE } }),
      { disableNotifications: true },
    );

    // The remote was switched while this page was loading.
    if (alias !== this._alias) {
      return;
    }

    this._history = data
      ? { state: 'loaded', transfers: data.transfers, nextBefore: data.nextBefore }
      : { state: 'failed', message: problemMessage(error, 'Reading the history failed.') };
  }

  async #loadMore() {
    const alias = this._alias;
    const history = this._history;
    if (!alias || history.state !== 'loaded' || history.nextBefore == null) {
      return;
    }

    this._history = { ...history, more: { state: 'loading' } };
    const { data, error } = await tryExecute(
      this,
      TransfrService.getHistory({ path: { alias }, query: { before: history.nextBefore, take: PAGE_SIZE } }),
      { disableNotifications: true },
    );
    if (alias !== this._alias) {
      return;
    }

    this._history = data
      ? { state: 'loaded', transfers: [...history.transfers, ...data.transfers], nextBefore: data.nextBefore }
      : { ...history, more: { state: 'failed', message: problemMessage(error, 'Reading more failed.') } };
  }

  // Closing the panel rejects the promise, which is not an error.
  #openDiff(document: TransferHistoryDocumentResponseModel) {
    const alias = this._alias;
    if (!alias || !document.name) {
      return;
    }

    const remote = this._remotes?.find((candidate) => candidate.alias === alias);
    umbOpenModal(this, TRANSFR_NODE_DIFF_MODAL, {
      data: { alias, remoteName: remote?.name ?? alias, key: document.key, name: document.name },
    }).catch(() => undefined);
  }

  override render() {
    if (!this._remotes) {
      return html`<uui-loader-bar></uui-loader-bar>`;
    }

    if (this._remotes.length === 0) {
      return html`<uui-box headline="History">
        <p>No remote to show the history of. Add or connect one under Remotes.</p>
      </uui-box>`;
    }

    const remoteName = this._remotes.find((candidate) => candidate.alias === this._alias)?.name ?? 'the remote';
    return html`
      <div class="toolbar">
        <uui-select
          label="Remote"
          .options=${this._remotes.map((candidate) => ({
            name: candidate.name,
            value: candidate.alias,
            selected: candidate.alias === this._alias,
          }))}
          @change=${(event: Event) => this.#selectRemote((event.target as UUISelectElement).value as string)}
        ></uui-select>
        <uui-button look="secondary" label="Refresh" @click=${() => this.#selectRemote(this._alias)}>
          <umb-icon name="icon-refresh"></umb-icon> Refresh
        </uui-button>
      </div>
      ${this.#renderHistory(remoteName)}
    `;
  }

  #renderHistory(remoteName: string) {
    const history = this._history;
    if (history.state === 'loading') {
      return html`<uui-loader-bar></uui-loader-bar>`;
    }

    if (history.state === 'failed') {
      return html`<div class="banner error" role="alert">
        <umb-icon name="icon-alert"></umb-icon>
        <span>${history.message}</span>
        <uui-button look="secondary" label="Retry" @click=${() => this.#selectRemote(this._alias)}></uui-button>
      </div>`;
    }

    if (history.transfers.length === 0) {
      return html`<uui-box headline="History">
        <p>Nothing has been copied from here to ${remoteName} yet.</p>
      </uui-box>`;
    }

    return html`
      <p class="intro">Every transfer from here to ${remoteName}, newest first, with the nodes it wrote.</p>
      <ol class="transfers">
        ${repeat(
          history.transfers,
          (transfer) => `${transfer.at}/${transfer.by ?? ''}`,
          (transfer) => this.#renderTransfer(transfer),
        )}
      </ol>
      ${this.#renderMore(history)}
    `;
  }

  #renderTransfer(transfer: TransferHistoryItemResponseModel) {
    return html`
      <li class="transfer">
        <div class="head">
          <umb-icon name="icon-history"></umb-icon>
          <strong>${describeTransferHeadline(transfer)}</strong>
          <small>${summarizeTransfer(transfer.documents)}</small>
        </div>
        <ul class="documents">
          ${repeat(
            transfer.documents,
            (document) => document.key,
            (document) => html`
              <li>
                <umb-icon name=${document.kind === 'Media' ? 'icon-picture' : 'icon-document'}></umb-icon>
                ${
                  document.name && document.kind === 'Media'
                    ? // Media have no diff panel yet: plain text, not styled like the clickable page names.
                      html`<span>${document.name}</span>`
                    : document.name
                      ? html`<button
                          type="button"
                          class="name"
                          title="Compare with the remote"
                          @click=${() => this.#openDiff(document)}
                        >
                          ${document.name}
                        </button>`
                      : html`<span class="gone">No longer here</span> <small>${document.key}</small>`
                }
                <uui-tag look="secondary">${outcomeWord(document.outcome)}</uui-tag>
              </li>
            `,
          )}
        </ul>
      </li>
    `;
  }

  #renderMore(history: Extract<History, { state: 'loaded' }>) {
    if (history.nextBefore == null) {
      return nothing;
    }

    return html`
      <div class="more">
        <uui-button
          look="secondary"
          label="Load more"
          .state=${history.more?.state === 'loading' ? 'waiting' : undefined}
          ?disabled=${history.more?.state === 'loading'}
          @click=${() => this.#loadMore()}
        ></uui-button>
        ${history.more?.state === 'failed' ? html`<span class="error" role="alert">${history.more.message}</span>` : nothing}
      </div>
    `;
  }

  static override styles = [
    brandStyles,
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .toolbar {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-5);
      }

      .intro {
        margin-top: 0;
        color: var(--uui-color-text-alt);
      }

      .transfers,
      .documents {
        list-style: none;
        margin: 0;
        padding: 0;
      }

      .transfer {
        background: var(--uui-color-surface);
        border: 1px solid var(--uui-color-border);
        border-left: 4px solid var(--transfr-color-remote);
        border-radius: var(--uui-border-radius);
        padding: var(--uui-size-space-4) var(--uui-size-space-5);
      }

      .transfer + .transfer {
        margin-top: var(--uui-size-space-4);
      }

      .head {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
      }

      .head umb-icon {
        color: var(--transfr-color-remote);
      }

      .head small {
        color: var(--uui-color-text-alt);
      }

      .documents {
        margin-top: var(--uui-size-space-3);
        padding-left: var(--uui-size-space-6);
      }

      .documents li {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        min-height: 30px;
      }

      .name {
        font: inherit;
        color: inherit;
        background: none;
        border: none;
        padding: 0;
        cursor: pointer;
        text-decoration: underline;
        text-underline-offset: 2px;
      }

      .name:hover,
      .name:focus-visible {
        color: var(--uui-color-interactive-emphasis);
      }

      .gone {
        font-style: italic;
        color: var(--uui-color-text-alt);
      }

      .documents small {
        color: var(--uui-color-text-alt);
      }

      .more {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-top: var(--uui-size-space-5);
      }

      .banner {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-4);
        border-left: 4px solid var(--uui-color-danger);
        background: var(--uui-color-surface);
      }

      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default TransfrHistoryElement;

declare global {
  interface HTMLElementTagNameMap {
    'transfr-history': TransfrHistoryElement;
  }
}
