import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type { CopyPlanResponseModel, PlanNoteResponseModel, PlanRowResponseModel } from '../api/index.js';
import { summarizeDifferences } from './content-summary.js';
import type { TransfrCopyPlanModalData, TransfrCopyPlanModalValue } from './copy-plan-modal.token.js';
import { OUTCOMES, countOutcomes, describeNote, describePublish, outcomeLook } from './plan-view.js';
import { problemMessage } from './problem.js';
import { describeSchemaProblem, describeSeparateInstall } from './schema-view.js';
import { addToSelection, describeSelection, fromRequest, toRequest } from './selection.js';
import type { Selection } from './selection.js';
import { formatDuration } from './subtree.js';

/**
 * The dry run of copying the selected nodes to a remote: what would happen to each node, and why. It reads both sides
 * and changes nothing. A node a block asks for (a missing parent or linked page that exists here) can be added to the
 * selection from here, which runs the dry run again; closing hands the selection back to the tree.
 */
@customElement('transfr-copy-plan-modal')
export class TransfrCopyPlanModalElement extends UmbModalBaseElement<
  TransfrCopyPlanModalData,
  TransfrCopyPlanModalValue
> {
  @state()
  private _selection: Selection = new Map();

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

  override connectedCallback() {
    super.connectedCallback();
    this._selection = fromRequest(this.data?.selection ?? []);
    void this.#load();
  }

  async #load() {
    const alias = this.data?.alias;
    if (!alias) {
      return;
    }

    this._loading = true;
    this._error = undefined;
    const started = performance.now();
    const { data, error } = await tryExecute(
      this,
      TransfrService.planCopy({ path: { alias }, body: { selection: toRequest(this._selection) } }),
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
    this._selection = addToSelection(this._selection, [key]);
    void this.#load();
  }

  #close() {
    this.updateValue({ selection: toRequest(this._selection) });
    this._submitModal();
  }

  override render() {
    const remote = this.data?.remoteName ?? 'the remote';
    return html`
      <umb-body-layout headline=${`Dry run: copy to ${remote}`}>
        <div class="intro">
          <umb-icon name="icon-info"></umb-icon>
          <span>A dry run reads both sides and changes nothing. ${describeSelection(this._selection)}.</span>
        </div>
        ${this.#renderBody(remote)}
        <div slot="actions">
          <uui-button look="primary" label="Close" @click=${() => this.#close()}></uui-button>
        </div>
      </umb-body-layout>
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
          ? html`<p class="empty">Everything selected is the same on ${remote}: nothing to copy.</p>`
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
          <small
            >${row.kind === 'Media' ? 'Media, planned along: copying media comes later' : (row.documentTypeName ?? '')}</small
          >
          <uui-tag color=${look.color} look=${look.look}
            ><span class="chip"><umb-icon name=${look.icon}></umb-icon>${look.label}</span></uui-tag
          >
        </div>
        ${differs ? html`<div class="line">Differs on ${remote}: ${differs}</div>` : nothing}
        ${publish ? html`<div class="line">${publish}</div>` : nothing}
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
    const canAdd = note.canAddToSelection && note.key && !this._selection.has(note.key);
    return html`
      <li class=${note.blocks ? 'blocks' : ''}>
        <umb-icon name=${note.blocks ? 'icon-block' : 'icon-info'}></umb-icon>
        <span>
          ${describeNote(note, remote)}
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
    css`
      .intro,
      .notice {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-5);
        padding: var(--uui-size-space-3) var(--uui-size-space-5);
        border-left: 4px solid var(--uui-color-selected);
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
