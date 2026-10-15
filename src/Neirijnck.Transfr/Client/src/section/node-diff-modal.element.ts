import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type {
  BlockDiffResponseModel,
  DocumentDiffResponseModel,
  PropertyDiffResponseModel,
  ValueDiffResponseModel,
} from '../api/index.js';
import { brandStyles } from './brand.js';
import { blockStatusLabel, fieldLabel, fieldValue } from './diff-labels.js';
import type { TransfrNodeDiffModalData } from './node-diff-modal.token.js';
import { problemMessage } from './problem.js';
import { diffWords } from './text-diff.js';
import type { DiffSegment } from './text-diff.js';

/**
 * One node on both sides, side by side: the document fields, then every property, blocks block by block. Changed
 * words are marked in place. By default only what differs is shown.
 */
@customElement('transfr-node-diff-modal')
export class TransfrNodeDiffModalElement extends UmbModalBaseElement<TransfrNodeDiffModalData, never> {
  @state()
  private _diff?: DocumentDiffResponseModel;

  @state()
  private _error?: string;

  @state()
  private _onlyDifferences = true;

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    if (!this.data) {
      return;
    }

    const { data, error } = await tryExecute(
      this,
      TransfrService.diffDocument({ path: { alias: this.data.alias, key: this.data.key } }),
      { disableNotifications: true },
    );
    if (data) {
      this._diff = data;
      return;
    }

    // The backoffice client replaces a 404's own message with a generic one, so say it here.
    this._error =
      (error as { status?: number } | undefined)?.status === 404
        ? 'This node is not on both sides any more. Refresh the tree.'
        : problemMessage(error, 'Comparing this node failed.');
  }

  override render() {
    const name = this.data?.name ?? '';
    const remote = this.data?.remoteName ?? 'the remote';
    return html`
      <umb-body-layout headline=${`${name}: this environment and ${remote}`}>
        ${this.#renderBody(remote)}
        <div slot="actions">
          <uui-button look="primary" label="Close" @click=${() => this._rejectModal()}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  #renderBody(remote: string) {
    if (this._error) {
      return html`<div class="notice error" role="alert"><umb-icon name="icon-alert"></umb-icon>${this._error}</div>`;
    }

    const diff = this._diff;
    if (!diff) {
      return html`<uui-loader-bar></uui-loader-bar>`;
    }

    const fields = diff.fields.filter((field) => !this._onlyDifferences || field.differs);
    const properties = diff.properties.filter((property) => !this._onlyDifferences || property.value.differs);
    return html`
      <div class="toolbar">
        ${
          diff.isIdentical
            ? html`<span class="notice same"><umb-icon name="icon-check"></umb-icon>Identical on both sides.</span>`
            : html`<span class="notice changed"
                ><umb-icon name="icon-edit"></umb-icon>${this.#countDifferences(diff)} differ.</span
              >`
        }
        ${
          diff.documentTypeName
            ? html`<span class="type"
                ><umb-icon name="icon-document-dashed-line"></umb-icon>Document type:
                <strong>${diff.documentTypeName}</strong></span
              >`
            : nothing
        }
        <uui-toggle
          label="Only differences"
          .checked=${this._onlyDifferences}
          @change=${(event: Event) => (this._onlyDifferences = (event.target as HTMLInputElement).checked)}
        ></uui-toggle>
      </div>

      ${
        fields.length > 0
          ? html`<uui-box headline="Node">
              ${this.#renderHead(remote)}
              ${repeat(
                fields,
                (field) => `${field.kind}|${field.culture}|${field.segment}`,
                (field) => html`
                  <div class="columns row ${field.differs ? 'differs' : ''}">
                    <span class="label">${fieldLabel(field)}</span>
                    ${this.#renderText(fieldValue(field, 'local'), fieldValue(field, 'remote'), field.differs, 'local')}
                    ${this.#renderText(fieldValue(field, 'local'), fieldValue(field, 'remote'), field.differs, 'remote')}
                  </div>
                `,
              )}
            </uui-box>`
          : nothing
      }
      ${
        properties.length > 0
          ? html`<uui-box headline="Properties"
              >${this.#renderHead(remote)}${properties.map((property) => this.#renderProperty(property, remote))}</uui-box
            >`
          : nothing
      }
    `;
  }

  // Column heads inside each box, so they line up with the rows below them.
  #renderHead(remote: string) {
    return html`<div class="columns row head" aria-hidden="true">
      <span></span>
      <span><umb-icon class="local" name="icon-home"></umb-icon>This environment</span>
      <span><umb-icon class="remote" name="icon-cloud"></umb-icon>${remote}</span>
    </div>`;
  }

  #countDifferences(diff: DocumentDiffResponseModel) {
    const count =
      diff.fields.filter((field) => field.differs).length + diff.properties.filter((p) => p.value.differs).length;
    return count === 1 ? '1 field or property' : `${count} fields and properties`;
  }

  #renderProperty(property: PropertyDiffResponseModel, remote: string): unknown {
    const where = [property.culture, property.segment].filter(Boolean).join(', ');
    const value = property.value;
    const label = html`<span class="label">
      ${property.label} ${where ? html`<span class="culture">${where}</span>` : nothing}
      ${property.label !== property.alias ? html`<small>${property.alias}</small>` : nothing}
    </span>`;

    return html`
      <div class="property ${value.differs ? 'differs' : ''}">
        ${
          (value.local !== null && value.local !== undefined) ||
          (value.remote !== null && value.remote !== undefined) ||
          value.blocks.length === 0
            ? html`<div class="columns row">
                ${label} ${this.#renderValue(value, 'local')} ${this.#renderValue(value, 'remote')}
              </div>`
            : html`<div class="columns row">${label}<span></span><span></span></div>`
        }
        ${value.blocks.length > 0 ? this.#renderBlocks(value.blocks, remote) : nothing}
      </div>
    `;
  }

  #renderValue(value: ValueDiffResponseModel, side: 'local' | 'remote') {
    return this.#renderText(value.local ?? undefined, value.remote ?? undefined, value.differs, side);
  }

  // One side of a text: equal texts plainly, differing texts with the removed (left) or added (right) words marked.
  #renderText(local: string | undefined, remote: string | undefined, differs: boolean, side: 'local' | 'remote') {
    const text = side === 'local' ? local : remote;
    if (text === undefined) {
      return html`<span class="value empty">${side === 'local' ? 'No value here' : 'No value there'}</span>`;
    }

    const code = /^\s*[[{<]/.test(text);
    if (!differs || local === undefined || remote === undefined) {
      return html`<span class="value ${code ? 'code' : ''}">${text}</span>`;
    }

    const segments: Array<DiffSegment> = diffWords(local, remote)[side];
    return html`<span class="value ${code ? 'code' : ''}"
      >${segments.map((segment) =>
        segment.kind === 'removed'
          ? html`<del>${segment.text}</del>`
          : segment.kind === 'added'
            ? html`<ins>${segment.text}</ins>`
            : segment.text,
      )}</span
    >`;
  }

  #renderBlocks(blocks: Array<BlockDiffResponseModel>, remote: string): unknown {
    const shown = blocks.filter((block) => !this._onlyDifferences || block.status !== 'Same');
    return html`<ol class="blocks">
      ${repeat(
        shown,
        (block) => block.key,
        (block) => {
          const values = block.values.filter((v) => !this._onlyDifferences || v.value.differs);
          const settings = block.settings.filter((v) => !this._onlyDifferences || v.value.differs);
          return html`
            <li class="block ${block.status}">
              <div class="block-head">
                <umb-icon name="icon-box"></umb-icon>
                <strong>${block.contentTypeName ?? 'Block'}</strong>
                <uui-tag
                  look=${block.status === 'Same' ? 'secondary' : 'primary'}
                  color=${this.#blockColor(block.status)}
                  >${blockStatusLabel(block, remote)}</uui-tag
                >
              </div>
              ${values.map((v) => this.#renderProperty(v, remote))}
              ${
                settings.length > 0
                  ? html`<div class="settings">Settings</div>
                      ${settings.map((v) => this.#renderProperty(v, remote))}`
                  : nothing
              }
            </li>
          `;
        },
      )}
    </ol>`;
  }

  #blockColor(status: string) {
    return status === 'OnlyLocal' || status === 'OnlyRemote' ? 'positive' : status === 'Moved' ? 'warning' : 'default';
  }

  static override styles = [
    brandStyles,
    css`
      .toolbar {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-5);
      }

      .toolbar uui-toggle {
        margin-left: auto;
      }

      .notice {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        font-weight: bold;
      }

      .notice.same {
        color: var(--uui-color-positive-standalone);
      }

      .notice.changed {
        color: var(--uui-color-selected);
      }

      .notice.error {
        color: var(--uui-color-danger);
      }

      .type {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      uui-box + uui-box {
        margin-top: var(--uui-size-space-5);
      }

      /* Label, this environment, the remote: the same three columns everywhere, so values line up. */
      .columns {
        display: grid;
        grid-template-columns: minmax(8rem, 1fr) minmax(0, 2fr) minmax(0, 2fr);
        gap: var(--uui-size-space-4);
        align-items: start;
      }

      .head {
        padding-top: 0;
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        font-weight: bold;
      }

      .head span {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .head umb-icon.local {
        color: var(--transfr-color-local);
      }

      .head umb-icon.remote {
        color: var(--transfr-color-remote);
      }

      .row {
        padding: var(--uui-size-space-3) var(--uui-size-space-3);
        border-left: 4px solid transparent;
        border-bottom: 1px solid var(--uui-color-divider);
      }

      .differs > .row,
      .row.differs {
        border-left-color: var(--uui-color-selected);
        background: color-mix(in srgb, var(--uui-color-selected) 6%, var(--uui-color-surface));
      }

      .label {
        display: flex;
        flex-direction: column;
        font-weight: bold;
        overflow-wrap: anywhere;
      }

      .label small {
        font-weight: normal;
        color: var(--uui-color-text-alt);
      }

      .culture {
        align-self: flex-start;
        padding: 0 var(--uui-size-space-2);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface-alt);
        font-size: var(--uui-type-small-size);
        font-weight: normal;
      }

      .value {
        white-space: pre-wrap;
        overflow-wrap: anywhere;
      }

      .value.code {
        font-family: var(--uui-font-monospace, monospace);
        font-size: var(--uui-type-small-size);
      }

      .value.empty {
        color: var(--uui-color-text-alt);
        font-style: italic;
      }

      /* Not only colour: removed words are struck through, added ones underlined. */
      del {
        background: color-mix(in srgb, var(--uui-color-danger) 18%, transparent);
        text-decoration: line-through;
      }

      ins {
        background: color-mix(in srgb, var(--uui-color-positive) 22%, transparent);
        text-decoration: underline;
      }

      /* No indent and no second accent inside a block: its own coloured edge shows the nesting, and the value columns
         stay in line with the ones above. */
      .blocks {
        list-style: none;
        margin: 0 0 var(--uui-size-space-3);
        padding: 0;
      }

      .block .row {
        border-left-width: 0;
      }

      .block {
        margin-top: var(--uui-size-space-3);
        border: 1px solid var(--uui-color-border);
        border-left-width: 4px;
        border-radius: var(--uui-border-radius);
      }

      .block.Changed {
        border-left-color: var(--uui-color-selected);
      }

      .block.Moved {
        border-left-color: var(--uui-color-warning);
      }

      .block.OnlyLocal,
      .block.OnlyRemote {
        border-left-color: var(--uui-color-positive);
      }

      .block-head {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-3);
        background: var(--uui-color-surface-alt);
      }

      .settings {
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        text-transform: uppercase;
      }
    `,
  ];
}

export default TransfrNodeDiffModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'transfr-node-diff-modal': TransfrNodeDiffModalElement;
  }
}
