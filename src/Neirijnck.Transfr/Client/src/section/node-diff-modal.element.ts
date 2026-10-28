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
import { blockStatusLabel, fieldLabel, fieldValue, isImageUrl } from './diff-labels.js';
import {
  batchButtonLabel,
  batchHint,
  hasPick,
  nameField,
  pickLabel,
  propertyField,
  samePicks,
  togglePick,
} from './field-picks.js';
import type { FieldPick } from './field-picks.js';
import type { TransfrNodeDiffModalData, TransfrNodeDiffModalValue } from './node-diff-modal.token.js';
import { problemMessage } from './problem.js';
import { diffWords } from './text-diff.js';
import type { DiffSegment } from './text-diff.js';

/**
 * One node on both sides, side by side: the document fields, then every property, blocks block by block. Changed
 * words are marked in place. By default only what differs is shown. Opened from the compare tree for a node that can
 * be copied, the names and properties that differ can be picked for a field-level copy and handed to the batch.
 */
@customElement('transfr-node-diff-modal')
export class TransfrNodeDiffModalElement extends UmbModalBaseElement<
  TransfrNodeDiffModalData,
  TransfrNodeDiffModalValue
> {
  @state()
  private _diff?: DocumentDiffResponseModel;

  @state()
  private _error?: string;

  @state()
  private _onlyDifferences = true;

  @state()
  private _picks: Array<FieldPick> = [];

  override connectedCallback() {
    super.connectedCallback();
    this._picks = [...(this.data?.batch?.fields ?? [])];
    void this.#load();
  }

  // Fields can be picked for a node the batch doesn't hold whole already.
  get #canPick() {
    return this.data?.batch !== undefined && !this.data.batch.whole;
  }

  #handBack() {
    this.updateValue({ fields: this._picks });
    this._submitModal();
  }

  async #load() {
    if (!this.data) {
      return;
    }

    const { data, error } = await tryExecute(
      this,
      this.#media
        ? TransfrService.diffMedia({ path: { alias: this.data.alias, key: this.data.key } })
        : TransfrService.diffDocument({ path: { alias: this.data.alias, key: this.data.key } }),
      { disableNotifications: true },
    );
    if (data) {
      this._diff = data;
      return;
    }

    // The backoffice client replaces a 404's own message with a generic one, so say it here.
    this._error =
      (error as { status?: number } | undefined)?.status === 404
        ? `This ${this.#media ? 'media item' : 'node'} is not on both sides any more. Refresh the tree.`
        : problemMessage(error, `Comparing this ${this.#media ? 'media item' : 'node'} failed.`);
  }

  get #media() {
    return this.data?.kind === 'Media';
  }

  override render() {
    const name = this.data?.name ?? '';
    const remote = this.data?.remoteName ?? 'the remote';
    return html`
      <umb-body-layout headline=${`${name}: this environment and ${remote}`}>
        ${this.#renderBody(remote)}
        <div slot="actions">${this.#renderActions()}</div>
      </umb-body-layout>
    `;
  }

  #renderActions() {
    if (!this.#canPick) {
      return html`<uui-button look="primary" label="Close" @click=${() => this._rejectModal()}></uui-button>`;
    }

    // No disabled button while the ticks match the batch: a hint says what to do, and Close is the way out.
    const before = this.data?.batch?.fields ?? [];
    if (samePicks(before, this._picks)) {
      return html`
        <span class="hint">${batchHint(before.length)}</span>
        <uui-button look="primary" label="Close" @click=${() => this._rejectModal()}></uui-button>
      `;
    }

    const label = batchButtonLabel(before.length, this._picks.length);
    return html`
      <uui-button look="secondary" label="Close" @click=${() => this._rejectModal()}></uui-button>
      <uui-button look="primary" label=${label} @click=${() => this.#handBack()}
        ><umb-icon name="icon-checkbox"></umb-icon> ${label}</uui-button
      >
    `;
  }

  // A checkbox before the label of a row that differs, when fields can be picked; an empty slot keeps rows in line.
  #renderPick(pick: FieldPick | undefined, differs: boolean, label: string) {
    if (!this.#canPick) {
      return nothing;
    }
    if (!pick || !differs) {
      return html`<span class="pick"></span>`;
    }

    const picked = hasPick(this._picks, pick);
    return html`<input
      type="checkbox"
      class="pick"
      aria-label=${pickLabel(label, this.data?.batch?.direction ?? 'ToRemote', this.data?.remoteName ?? 'the remote')}
      title=${picked ? `${label} goes to the batch` : `Pick ${label} for a field-level copy`}
      .checked=${picked}
      @change=${() => (this._picks = togglePick(this._picks, pick))}
    />`;
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
                ><umb-icon name="icon-document-dashed-line"></umb-icon>${this.#media ? 'Media type' : 'Document type'}:
                <strong>${diff.documentTypeName}</strong></span
              >`
            : nothing
        }
        ${
          this.data?.batch?.whole
            ? html`<span class="type"
                ><umb-icon name="icon-checkbox"></umb-icon>In the batch as a whole node: all its fields come
                along.</span
              >`
            : nothing
        }
        <uui-toggle
          label="Only differences"
          .checked=${this._onlyDifferences}
          @change=${(event: Event) => (this._onlyDifferences = (event.target as HTMLInputElement).checked)}
        ></uui-toggle>
      </div>

      ${this.#renderFiles(diff, remote)}
      ${
        fields.length > 0
          ? html`<uui-box headline=${this.#media ? 'Media item' : 'Node'}>
              ${this.#renderHead(remote)}
              ${repeat(
                fields,
                (field) => `${field.kind}|${field.culture}|${field.segment}`,
                (field) => html`
                  <div class="columns row ${field.differs ? 'differs' : ''}">
                    <span class="label-cell"
                      >${this.#renderPick(nameField(field), field.differs, fieldLabel(field))}<span class="label"
                        >${fieldLabel(field)}</span
                      ></span
                    >
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
              >${this.#renderHead(remote)}${properties.map((property) => this.#renderProperty(property, remote, true))}</uui-box
            >`
          : nothing
      }
    `;
  }

  // A media item's files on both sides, next to each other: an image shown, any other file as a link. Shown whether
  // or not they differ: seeing the two is the point.
  #renderFiles(diff: DocumentDiffResponseModel, remote: string) {
    const files = diff.files ?? [];
    if (files.length === 0) {
      return nothing;
    }

    const show = (url: string | null | undefined, side: string) =>
      !url
        ? html`<span class="empty">No file ${side}</span>`
        : isImageUrl(url)
          ? html`<a class="file" href=${url} target="_blank" rel="noopener noreferrer" title="Open the file"
              ><img src=${url} alt=${`The file ${side}`} loading="lazy"
            /></a>`
          : html`<a class="file" href=${url} target="_blank" rel="noopener noreferrer">${url.split('/').pop()}</a>`;
    return html`<uui-box headline="Files">
      ${this.#renderHead(remote)}
      ${files.map(
        (file) =>
          html`<div class="columns row">
            <span class="label-cell"><span class="label">${file.label}</span></span>
            <span class="value local">${show(file.localUrl, 'here')}</span>
            <span class="value remote">${show(file.remoteUrl, `on ${remote}`)}</span>
          </div>`,
      )}
    </uui-box>`;
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

  // A property of the node can be picked as a field (blocks and all); a value inside a block can't on its own.
  #renderProperty(property: PropertyDiffResponseModel, remote: string, pickable = false): unknown {
    const where = [property.culture, property.segment].filter(Boolean).join(', ');
    const value = property.value;
    const named = where ? `${property.label} (${where})` : property.label;
    const label = html`<span class="label-cell"
      >${pickable ? this.#renderPick(propertyField(property), value.differs, named) : nothing}<span class="label">
        ${property.label} ${where ? html`<span class="culture">${where}</span>` : nothing}
        ${property.label !== property.alias ? html`<small>${property.alias}</small>` : nothing}
      </span></span
    >`;

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

      /* A media file on each side: the image at a size both fit side by side, on a neutral backdrop for transparency. */
      .file img {
        display: block;
        max-width: 100%;
        max-height: 240px;
        object-fit: contain;
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface-alt);
      }

      .empty {
        font-style: italic;
        color: var(--uui-color-text-alt);
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

      .hint {
        align-self: center;
        margin-right: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
      }

      .label-cell {
        display: flex;
        align-items: flex-start;
        gap: var(--uui-size-space-3);
        min-width: 0;
      }

      .label {
        display: flex;
        flex-direction: column;
        font-weight: bold;
        overflow-wrap: anywhere;
      }

      /* The tree's pick colour (the icon's amber), so a pick looks the same wherever it is made. */
      .pick {
        flex: none;
        width: 1rem;
        height: 1rem;
        margin: 0.15rem 0 0;
        accent-color: var(--transfr-color-accent);
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
