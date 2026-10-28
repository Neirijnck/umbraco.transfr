import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type {
  CopyPlanResponseModel,
  PlanNoteResponseModel,
  PlanRowResponseModel,
  TransferResponseModel,
  TransferRowResponseModel,
} from '../api/index.js';
import { brandStyles } from './brand.js';
import { summarizeDifferences } from './content-summary.js';
import type { TransfrCopyPlanModalData, TransfrCopyPlanModalValue } from './copy-plan-modal.token.js';
import { sidesOf, type Direction } from './direction.js';
import { describeFields } from './field-picks.js';
import {
  OUTCOMES,
  countOutcomes,
  describeLastCopy,
  describeNote,
  describePublish,
  describeRemoteChange,
  outcomeLook,
} from './plan-view.js';
import { problemMessage } from './problem.js';
import { renderRoute, routeStyles } from './route.js';
import { describeSchemaProblem, describeSeparateInstall } from './schema-view.js';
import { EMPTY_BATCH, addToSelection, batchFromRequest, batchToRequest, describeBatch } from './selection.js';
import type { Batch } from './selection.js';
import { formatDuration } from './subtree.js';
import { countWrites, describeDrift, describeTransfer, describeWrites, resultLook, toDryRun } from './transfer-view.js';

/**
 * The dry run of copying the selected nodes to a remote: what would happen to each node, and why. It reads both sides
 * and changes nothing. A node a block asks for (a missing parent or linked page that exists here) can be added to the
 * selection from here, which runs the dry run again. Conflicts can be chosen to overwrite; Transfer then carries out
 * exactly this dry run (the server refuses when anything changed since) and shows what happened per node. Closing hands
 * the selection back to the tree.
 */
@customElement('transfr-copy-plan-modal')
export class TransfrCopyPlanModalElement extends UmbModalBaseElement<
  TransfrCopyPlanModalData,
  TransfrCopyPlanModalValue
> {
  @state()
  private _batch: Batch = EMPTY_BATCH;

  @state()
  private _plan?: CopyPlanResponseModel;

  @state()
  private _milliseconds = 0;

  @state()
  private _error?: string;

  @state()
  private _loading = false;

  @state()
  private _hideUnchanged = true;

  // Conflicts chosen to overwrite, by key; cleared with every new dry run.
  @state()
  private _overwrite: ReadonlySet<string> = new Set();

  @state()
  private _transferring = false;

  @state()
  private _transfer?: TransferResponseModel;

  override connectedCallback() {
    super.connectedCallback();
    this._batch = batchFromRequest(this.data?.selection ?? []);
    void this.#load();
  }

  get #direction(): Direction {
    return this.data?.direction ?? 'ToRemote';
  }

  async #load() {
    const alias = this.data?.alias;
    if (!alias) {
      return;
    }

    this._loading = true;
    this._error = undefined;
    this._transfer = undefined;
    this._overwrite = new Set();
    const started = performance.now();
    const { data, error } = await tryExecute(
      this,
      TransfrService.planCopy({
        path: { alias },
        body: { selection: batchToRequest(this._batch), direction: this.#direction },
      }),
      { disableNotifications: true },
    );
    this._loading = false;
    this._milliseconds = performance.now() - started;
    if (data) {
      this._plan = data;
      return;
    }

    this._plan = undefined;
    this._error = problemMessage(error, 'The dry run failed.');
  }

  #add(key: string) {
    this._batch = { ...this._batch, documents: addToSelection(this._batch.documents, [key]) };
    void this.#load();
  }

  #toggleOverwrite(key: string, overwrite: boolean) {
    const next = new Set(this._overwrite);
    if (overwrite) {
      next.add(key);
    } else {
      next.delete(key);
    }
    this._overwrite = next;
  }

  // Carries out the dry run on screen; the server plans again and refuses when anything changed since.
  async #transfer() {
    const alias = this.data?.alias;
    const plan = this._plan;
    if (!alias || !plan) {
      return;
    }

    this._transferring = true;
    this._error = undefined;
    const { data, error } = await tryExecute(
      this,
      TransfrService.transfer({
        path: { alias },
        body: {
          selection: batchToRequest(this._batch),
          dryRun: toDryRun(plan.rows),
          overwrite: [...this._overwrite],
          direction: this.#direction,
        },
      }),
      { disableNotifications: true },
    );
    this._transferring = false;
    if (data) {
      this._transfer = data;
      this.data?.onTransferred?.(data);
      return;
    }

    this._error = problemMessage(error, 'The transfer failed.');
  }

  #close() {
    this.updateValue({ selection: this._transfer?.status === 'Completed' ? [] : batchToRequest(this._batch) });
    this._submitModal();
  }

  override render() {
    const remote = this.data?.remoteName ?? 'the remote';
    const plan = this._plan;
    const writes = plan ? countWrites(plan.rows, this._overwrite) : 0;
    const refused = (plan?.separateInstall.length ?? 0) > 0;
    const { way } = sidesOf(this.#direction, remote);
    return html`
      <umb-body-layout headline=${this._transfer ? `Transfer ${way}` : `Dry run: copy ${way}`}>
        ${renderRoute(this.#direction, remote)}
        ${
          this._transfer
            ? this.#renderReport(this._transfer, remote)
            : html`<div class="intro">
                  <umb-icon name="icon-info"></umb-icon>
                  <span>A dry run reads both sides and changes nothing. ${describeBatch(this._batch)}.</span>
                </div>
                ${this.#renderBody(remote)}`
        }
        <div slot="actions">
          ${
            this._transfer
              ? this._transfer.status === 'Refused' || this._transfer.status === 'Busy'
                ? html`<uui-button look="secondary" label="Dry run again" @click=${() => this.#load()}></uui-button>`
                : nothing
              : html`<uui-button
                  look="primary"
                  color="positive"
                  label=${`Transfer ${plan ? describeWrites(plan.rows, this._overwrite) : '0 nodes'} ${way}`}
                  .state=${this._transferring ? 'waiting' : undefined}
                  ?disabled=${writes === 0 || refused || this._loading || this._transferring || !plan}
                  @click=${() => this.#transfer()}
                ></uui-button>`
          }
          <uui-button
            look=${this._transfer ? 'primary' : 'secondary'}
            label="Close"
            ?disabled=${this._transferring}
            @click=${() => this.#close()}
          ></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  #renderReport(transfer: TransferResponseModel, remote: string) {
    const failed = transfer.status !== 'Completed';
    const rows = transfer.rows.filter((row) => !this._hideUnchanged || row.result !== 'Unchanged');
    return html`
      <div class="notice ${failed ? 'error' : 'done'}" role=${failed ? 'alert' : 'status'}>
        <umb-icon name=${failed ? 'icon-alert' : 'icon-check'}></umb-icon>
        <span>${describeTransfer(transfer, remote, this.#direction)}</span>
      </div>
      ${transfer.error && transfer.status !== 'Busy' ? html`<p class="error-text">${transfer.error}</p>` : nothing}
      ${
        transfer.drift.length > 0
          ? html`<ul class="notes drift">
              ${transfer.drift.map(
                (drift) =>
                  html`<li class="blocks">
                    <umb-icon name="icon-block"></umb-icon><span>${describeDrift(drift, remote)}</span>
                  </li>`,
              )}
            </ul>`
          : nothing
      }
      ${
        transfer.rows.length > 0
          ? html`<div class="summary">
                <small>${transfer.remoteRequests} requests to ${remote}</small>
                <uui-toggle
                  label="Hide unchanged"
                  .checked=${this._hideUnchanged}
                  @change=${(event: Event) => (this._hideUnchanged = (event.target as HTMLInputElement).checked)}
                ></uui-toggle>
              </div>
              <ol class="rows">
                ${repeat(
                  rows,
                  (row) => `${row.kind}/${row.key}`,
                  (row) => this.#renderResult(row, remote),
                )}
              </ol>`
          : nothing
      }
    `;
  }

  #renderResult(row: TransferRowResponseModel, remote: string) {
    const look = resultLook(row.result);
    return html`
      <li class="row ${row.result}" data-key=${row.key}>
        <div class="head">
          <umb-icon class="kind" name=${row.kind === 'Media' ? 'icon-picture' : 'icon-document'}></umb-icon>
          <strong class="name">${row.name}</strong>
          <small>${row.outcome}</small>
          <uui-tag color=${look.color} look=${look.look}
            ><span class="chip"><umb-icon name=${look.icon}></umb-icon>${look.label}</span></uui-tag
          >
        </div>
        ${row.targetState ? html`<div class="line">${sidesOf(this.#direction, remote).targetNow}: ${row.targetState}</div>` : nothing}
        ${row.error ? html`<div class="line error-text">${row.error}</div>` : nothing}
      </li>
    `;
  }

  #renderBody(remote: string) {
    if (this._error) {
      return html`<div class="notice error" role="alert">
        <umb-icon name="icon-alert"></umb-icon><span>${this._error}</span>
        <uui-button look="secondary" compact label="Retry" @click=${() => this.#load()}></uui-button>
      </div>`;
    }

    const plan = this._plan;
    if (!plan) {
      return html`<uui-loader-bar></uui-loader-bar>`;
    }

    const counts = countOutcomes(plan.rows);
    const unknown = [...counts.keys()].filter((outcome) => !(OUTCOMES as ReadonlyArray<string>).includes(outcome));
    const rows = plan.rows.filter((row) => !this._hideUnchanged || row.outcome !== 'Unchanged');
    return html`
      ${this._loading ? html`<uui-loader-bar></uui-loader-bar>` : nothing}
      ${
        plan.separateInstall.length > 0
          ? html`<div class="notice error" role="alert">
              <umb-icon name="icon-block"></umb-icon
              ><span>${describeSeparateInstall(plan.separateInstall, remote)}</span>
            </div>`
          : nothing
      }
      <div class="summary" role="status">
        ${[...OUTCOMES, ...unknown]
          .filter((outcome) => counts.has(outcome))
          .map((outcome) => {
            const look = outcomeLook(outcome);
            return html`<uui-tag color=${look.color} look=${look.look}
              ><span class="chip"
                ><umb-icon name=${look.icon}></umb-icon>${counts.get(outcome)} ${look.label}</span
              ></uui-tag
            >`;
          })}
        <small>${plan.remoteRequests} requests to ${remote}, ${formatDuration(this._milliseconds)}</small>
        ${
          counts.has('Unchanged')
            ? html`<uui-toggle
                label="Hide unchanged"
                .checked=${this._hideUnchanged}
                @change=${(event: Event) => (this._hideUnchanged = (event.target as HTMLInputElement).checked)}
              ></uui-toggle>`
            : nothing
        }
      </div>
      ${
        rows.length === 0
          ? html`<p class="empty">
              Everything selected is the same ${sidesOf(this.#direction, remote).onTarget}: nothing to copy.
            </p>`
          : html`<ol class="rows">
              ${repeat(
                rows,
                (row) => `${row.kind}/${row.key}`,
                (row) => this.#renderRow(row, remote),
              )}
            </ol>`
      }
    `;
  }

  #renderRow(row: PlanRowResponseModel, remote: string) {
    const look = outcomeLook(row.outcome);
    const publish = describePublish(row);
    const differs = row.differences.length > 0 ? summarizeDifferences(row.differences, 5) : undefined;
    return html`
      <li class="row ${row.outcome}" data-key=${row.key}>
        <div class="head">
          <umb-icon class="kind" name=${row.kind === 'Media' ? 'icon-picture' : 'icon-document'}></umb-icon>
          <strong class="name">${row.name}</strong>
          <small>${row.documentTypeName ?? (row.kind === 'Media' ? 'Media' : '')}</small>
          <uui-tag color=${look.color} look=${look.look}
            ><span class="chip"><umb-icon name=${look.icon}></umb-icon>${look.label}</span></uui-tag
          >
        </div>
        ${row.fields?.length ? html`<div class="line">Only these fields: ${describeFields(row.fields)}</div>` : nothing}
        ${differs ? html`<div class="line">Differs ${sidesOf(this.#direction, remote).onTarget}: ${differs}</div>` : nothing}
        ${
          row.changedOnRemote
            ? html`<div class="line">
                ${describeRemoteChange(row.changedOnRemote, remote, undefined, this.#direction)}
              </div>`
            : nothing
        }
        ${row.lastCopied ? html`<div class="line">${describeLastCopy(row.lastCopied, remote)}</div>` : nothing}
        ${publish ? html`<div class="line">${publish}</div>` : nothing}
        ${
          row.outcome === 'Conflict'
            ? html`<label class="line overwrite">
                <input
                  type="checkbox"
                  .checked=${this._overwrite.has(row.key)}
                  ?disabled=${this._transferring}
                  @change=${(event: Event) => this.#toggleOverwrite(row.key, (event.target as HTMLInputElement).checked)}
                />
                ${sidesOf(this.#direction, remote).overwrite}
              </label>`
            : nothing
        }
        ${
          row.notes.length > 0
            ? html`<ul class="notes">
                ${row.notes.map((note) => this.#renderNote(note, row, remote))}
              </ul>`
            : nothing
        }
      </li>
    `;
  }

  #renderNote(note: PlanNoteResponseModel, row: PlanRowResponseModel, remote: string) {
    const canAdd = note.canAddToSelection && note.key && !this._batch.documents.has(note.key);
    return html`
      <li class=${note.blocks ? 'blocks' : ''}>
        <umb-icon name=${note.blocks ? 'icon-block' : 'icon-info'}></umb-icon>
        <span>
          ${describeNote(note, remote, row.kind, this.#direction)}
          ${
            note.kind === 'SchemaMismatch'
              ? html`<ul class="details">
                  ${row.schemaProblems.map((problem) => html`<li>${describeSchemaProblem(problem, remote)}</li>`)}
                </ul>`
              : nothing
          }
        </span>
        ${
          canAdd
            ? html`<uui-button
                look="secondary"
                compact
                label=${`Add ${note.name ?? 'it'} to the selection`}
                ?disabled=${this._loading}
                @click=${() => this.#add(note.key!)}
                ><span class="chip"><umb-icon name="icon-add"></umb-icon>Add to selection</span></uui-button
              >`
            : nothing
        }
      </li>
    `;
  }

  static override styles = [
    brandStyles,
    routeStyles,
    css`
      .intro,
      .notice {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-5);
        padding: var(--uui-size-space-3) var(--uui-size-space-5);
        border-left: 4px solid var(--transfr-color-brand);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      .intro > span,
      .notice > span {
        flex: 1;
      }

      .notice.error {
        border-left-color: var(--uui-color-danger);
        color: var(--uui-color-danger);
      }

      .notice.done {
        border-left-color: var(--uui-color-positive);
      }

      .error-text {
        color: var(--uui-color-danger);
      }

      .overwrite {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        cursor: pointer;
      }

      .overwrite input {
        margin: 0;
        accent-color: var(--uui-color-warning-emphasis, var(--uui-color-warning));
      }

      .drift {
        margin-bottom: var(--uui-size-space-5);
      }

      /* Transfer results, in the same cards as the dry run; the result is in the tag's text too. */
      .row.Written {
        border-left-color: var(--uui-color-positive);
      }

      .row.Failed {
        border-left-color: var(--uui-color-danger);
      }

      .row.Skipped {
        border-left-color: var(--uui-color-warning);
      }

      .summary {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-5);
      }

      .summary small {
        color: var(--uui-color-text-alt);
      }

      .summary uui-toggle {
        margin-left: auto;
      }

      /* Block-level flex, as in the tree: an inline-flex box sits on the baseline and leaves the content off-centre. */
      .chip {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-1);
        white-space: nowrap;
      }

      .rows,
      .notes,
      .details {
        list-style: none;
        margin: 0;
        padding: 0;
      }

      .row {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-2);
        margin-bottom: var(--uui-size-space-3);
        padding: var(--uui-size-space-3) var(--uui-size-space-4);
        border: 1px solid var(--uui-color-border);
        border-left-width: 4px;
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      /* The outcome is in the tag's text too, so it's never told by colour alone. */
      .row.Create {
        border-left-color: var(--uui-color-positive);
      }

      .row.Update {
        border-left-color: var(--uui-color-selected);
      }

      .row.Conflict {
        border-left-color: var(--uui-color-warning);
      }

      .row.Blocked {
        border-left-color: var(--uui-color-danger);
        background: var(--uui-color-disabled);
      }

      .head {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        min-width: 0;
      }

      .head small {
        color: var(--uui-color-text-alt);
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .head uui-tag {
        margin-left: auto;
      }

      .kind {
        flex-shrink: 0;
        color: var(--uui-color-text-alt);
      }

      .name {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .line,
      .notes {
        padding-left: calc(1em + var(--uui-size-space-3));
      }

      .notes li {
        display: flex;
        align-items: flex-start;
        gap: var(--uui-size-space-2);
        padding: var(--uui-size-space-1) 0;
      }

      .notes li > span {
        flex: 1;
      }

      .notes umb-icon {
        flex-shrink: 0;
        margin-top: 0.2em;
        color: var(--uui-color-text-alt);
      }

      .notes li.blocks,
      .notes li.blocks umb-icon {
        color: var(--uui-color-danger);
      }

      .details li {
        padding: 0;
        color: var(--uui-color-text-alt);
      }

      .details li::before {
        content: '– ';
      }

      .empty {
        color: var(--uui-color-text-alt);
        font-style: italic;
      }
    `,
  ];
}

export default TransfrCopyPlanModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'transfr-copy-plan-modal': TransfrCopyPlanModalElement;
  }
}
