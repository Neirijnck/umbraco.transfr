import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUISelectElement } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type { RemoteResponseModel, TreeRowResponseModel } from '../api/index.js';
import { problemMessage } from './problem.js';
import { rowView } from './tree-row-view.js';
import type { SideKind } from './tree-row-view.js';

type Level =
  { state: 'loading' } | { state: 'failed'; message: string } | { state: 'loaded'; rows: Array<TreeRowResponseModel> };

type Side = 'local' | 'remote';

// The root level has no parent key; '' stands for it in the level and expansion maps.
const ROOT = '';

/**
 * Content compare view: this environment's content tree next to a remote's, compared by key one level at a time.
 * Rows line up across both trees; every difference is drawn in the trees themselves (accent, chip, placeholder), so
 * identical nodes stay quiet and differences stand out. A level is read when its parent is expanded and kept until the
 * remote changes or the view is refreshed.
 */
@customElement('transfr-content-compare')
export class TransfrContentCompareElement extends UmbLitElement {
  @state()
  private _remotes?: Array<RemoteResponseModel>;

  @state()
  private _alias?: string;

  // Levels by parent key: the same node can show up twice (moved), and both places share its children.
  @state()
  private _levels: Record<string, Level> = {};

  // Expanded rows by position (parent key + key), so expanding a moved node in one place leaves the other closed.
  @state()
  private _expanded: Set<string> = new Set();

  // The row under the pointer or focus, so its namesake with another key lights up too.
  @state()
  private _hoverKey?: string;

  // The position just revealed by "Show", highlighted for a moment.
  @state()
  private _flash?: string;

  override connectedCallback() {
    super.connectedCallback();
    void this.#loadRemotes();
  }

  async #loadRemotes() {
    const { data } = await tryExecute(this, TransfrService.getRemotes());
    this._remotes = (data?.remotes ?? []).filter((remote) => remote.isValid);
    this.#selectRemote(this._remotes[0]?.alias);
  }

  #selectRemote(alias: string | undefined) {
    this._alias = alias;
    this._levels = {};
    this._expanded = new Set();
    if (alias) {
      void this.#loadLevel(ROOT);
    }
  }

  async #loadLevel(parentKey: string) {
    const alias = this._alias;
    if (!alias) {
      return;
    }

    this._levels = { ...this._levels, [parentKey]: { state: 'loading' } };
    const { data, error } = await tryExecute(
      this,
      TransfrService.compareTreeLevel({
        path: { alias },
        query: parentKey === ROOT ? undefined : { parentKey },
      }),
      { disableNotifications: true },
    );

    // The remote was switched while this level was loading: its result belongs to the old tree.
    if (alias !== this._alias) {
      return;
    }

    this._levels = {
      ...this._levels,
      [parentKey]: data
        ? { state: 'loaded', rows: data.rows }
        : { state: 'failed', message: problemMessage(error, 'Comparing this level failed.') },
    };
  }

  #toggle(parentKey: string, key: string) {
    const position = `${parentKey}/${key}`;
    const expanded = new Set(this._expanded);
    if (expanded.delete(position)) {
      this._expanded = expanded;
      return;
    }

    expanded.add(position);
    this._expanded = expanded;
    if (!this._levels[key] || this._levels[key].state === 'failed') {
      void this.#loadLevel(key);
    }
  }

  // The loaded level a node's row is in (the first one, if it shows up twice), or undefined when it isn't loaded yet.
  #findRow(key: string): { levelKey: string; row: TreeRowResponseModel } | undefined {
    for (const [levelKey, level] of Object.entries(this._levels)) {
      const row = level.state === 'loaded' ? level.rows.find((candidate) => candidate.key === key) : undefined;
      if (row) {
        return { levelKey, row };
      }
    }
    return undefined;
  }

  // Expands the path down to the node's other place, scrolls to it and highlights it.
  async #reveal(otherParentKey: string | null | undefined, key: string) {
    const parent = otherParentKey ?? ROOT;
    const expanded = new Set(this._expanded);
    for (let current = parent; current !== ROOT;) {
      const found = this.#findRow(current);
      if (!found) {
        return;
      }
      expanded.add(`${found.levelKey}/${current}`);
      current = found.levelKey;
    }
    this._expanded = expanded;

    if (this._levels[parent]?.state !== 'loaded') {
      await this.#loadLevel(parent);
    }
    await this.updateComplete;

    const position = `${parent}/${key}`;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    this.shadowRoot
      ?.querySelector(`li[data-position="${position}"]`)
      ?.scrollIntoView({ block: 'center', behavior: reduceMotion ? 'auto' : 'smooth' });
    this._flash = position;
    setTimeout(() => {
      if (this._flash === position) {
        this._flash = undefined;
      }
    }, 2000);
  }

  override render() {
    if (!this._remotes) {
      return html`<uui-loader-bar></uui-loader-bar>`;
    }

    if (this._remotes.length === 0) {
      return html`<uui-box headline="Content">
        <p>No remote to compare with. Add or connect one under Remotes.</p>
      </uui-box>`;
    }

    const remote = this._remotes.find((candidate) => candidate.alias === this._alias);
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
        ${this.#renderLegend()}
      </div>
      <div class="panes">
        <div class="pane-head local">
          <umb-icon name="icon-home"></umb-icon>
          <div>
            <strong>This environment</strong>
            <small>${window.location.host}</small>
          </div>
        </div>
        <div class="pane-head remote">
          <umb-icon name="icon-cloud"></umb-icon>
          <div>
            <strong>${remote?.name}</strong>
            <small>${remote?.baseUrl}</small>
          </div>
        </div>
        <div class="tree">${this.#renderLevel(ROOT, 0, remote?.name ?? 'the remote')}</div>
      </div>
    `;
  }

  #renderLegend() {
    return html`
      <ul class="legend" aria-label="Legend">
        <li><span class="swatch same"></span> Same place in both</li>
        <li><span class="swatch only"></span> Only on this side</li>
        <li><span class="swatch missing"></span> Not on this side</li>
        <li><span class="swatch moved"></span> Under another parent on the other side</li>
        <li><span class="swatch conflict"></span> Same name, other key</li>
      </ul>
    `;
  }

  #renderLevel(parentKey: string, depth: number, remoteName: string): unknown {
    const level = this._levels[parentKey];
    if (!level || level.state === 'loading') {
      return html`<div class="message" style="--depth: ${depth}"><uui-loader-bar></uui-loader-bar></div>`;
    }

    if (level.state === 'failed') {
      return html`<div class="message error" style="--depth: ${depth}" data-test="level-error">
        <umb-icon name="icon-alert"></umb-icon>
        <span>${level.message}</span>
        <uui-button look="secondary" compact label="Retry" @click=${() => this.#loadLevel(parentKey)}></uui-button>
      </div>`;
    }

    if (level.rows.length === 0) {
      return html`<div class="message empty" style="--depth: ${depth}">
        ${parentKey === ROOT ? 'Both content trees are empty.' : 'No child nodes on either side.'}
      </div>`;
    }

    return html`<ul>
      ${repeat(
        level.rows,
        (row) => row.key,
        (row) => this.#renderRow(parentKey, row, depth, remoteName),
      )}
    </ul>`;
  }

  #renderRow(parentKey: string, row: TreeRowResponseModel, depth: number, remoteName: string) {
    const position = `${parentKey}/${row.key}`;
    const expanded = this._expanded.has(position);
    const view = rowView(row.status);
    const paired = this._hoverKey !== undefined && row.sameNameKey === this._hoverKey;
    const classes = ['row', paired ? 'paired' : '', this._flash === position ? 'flash' : ''].join(' ');

    return html`
      <li data-key=${row.key} data-status=${row.status} data-position=${position}>
        <div
          class=${classes}
          style="--depth: ${depth}"
          @mouseenter=${() => (this._hoverKey = row.key)}
          @mouseleave=${() => (this._hoverKey = undefined)}
          @focusin=${() => (this._hoverKey = row.key)}
          @focusout=${() => (this._hoverKey = undefined)}
        >
          ${this.#renderCell('local', parentKey, row, view.local, view.known, expanded, remoteName)}
          ${this.#renderCell('remote', parentKey, row, view.remote, view.known, expanded, remoteName)}
        </div>
        ${expanded ? this.#renderLevel(row.key, depth + 1, remoteName) : nothing}
      </li>
    `;
  }

  #renderCell(
    side: Side,
    parentKey: string,
    row: TreeRowResponseModel,
    kind: SideKind,
    known: boolean,
    expanded: boolean,
    remoteName: string,
  ) {
    const node = side === 'local' ? row.local : row.remote;
    const name = row.local?.name ?? row.remote?.name ?? row.key;
    const hasChildren = Boolean(row.local?.hasChildren || row.remote?.hasChildren);

    // One expander per row, in the left tree: expanding opens the level on both sides.
    const expander =
      side !== 'local'
        ? html`<span class="expander"></span>`
        : hasChildren
          ? html`<uui-button
              class="expander"
              compact
              label=${`${expanded ? 'Collapse' : 'Expand'} ${name}`}
              @click=${() => this.#toggle(parentKey, row.key)}
              ><uui-symbol-expand .open=${expanded}></uui-symbol-expand
            ></uui-button>`
          : html`<span class="expander"></span>`;

    return html`
      <div class="cell ${side} ${kind}">
        ${expander}
        ${
          node && kind !== 'missing' && kind !== 'elsewhere'
            ? html`
                <umb-icon class="doc" name="icon-document"></umb-icon>
                <span class="name">${node.name}</span>
                <span class="chips">${this.#renderChips(side, row, kind, known, remoteName)}</span>
              `
            : this.#renderPlaceholder(side, row, kind, remoteName)
        }
      </div>
    `;
  }

  // Chips only for differences: identical nodes stay quiet. The schema check adds its "blocked" chip here later.
  #renderChips(side: Side, row: TreeRowResponseModel, kind: SideKind, known: boolean, remoteName: string) {
    return html`
      ${
        kind === 'only'
          ? this.#chip('positive', 'icon-add', side === 'local' ? 'Only here' : `Only on ${remoteName}`)
          : nothing
      }
      ${
        kind === 'moved'
          ? this.#chip(
              'warning',
              'icon-shuffle',
              side === 'local' ? `Other parent on ${remoteName}` : 'Other parent here',
            )
          : nothing
      }
      ${
        row.sameNameKey
          ? this.#chip(
              'danger',
              'icon-alert',
              'Same name, other key',
              'The other tree has a node with this name but another key under this parent. Copying it would be blocked.',
            )
          : nothing
      }
      ${known ? nothing : html`<uui-tag look="outline">${row.status}</uui-tag>`}
    `;
  }

  #chip(color: 'positive' | 'warning' | 'danger', icon: string, text: string, title?: string) {
    return html`<uui-tag color=${color} look="primary" title=${title ?? text}
      ><span class="chip"><umb-icon name=${icon}></umb-icon>${text}</span></uui-tag
    >`;
  }

  #renderPlaceholder(side: Side, row: TreeRowResponseModel, kind: SideKind, remoteName: string) {
    // A faded icon keeps the placeholder text in line with the node names around it.
    const icon = html`<umb-icon class="doc faded" name="icon-document"></umb-icon>`;
    if (kind !== 'elsewhere') {
      return html`${icon}<span class="placeholder">${side === 'local' ? 'Not here' : `Not on ${remoteName}`}</span>`;
    }

    // The node lives under another parent on this side; name that parent when its row is loaded somewhere.
    const parentRow = row.otherParentKey ? this.#findRow(row.otherParentKey)?.row : undefined;
    const parentName =
      side === 'local'
        ? (parentRow?.local?.name ?? parentRow?.remote?.name)
        : (parentRow?.remote?.name ?? parentRow?.local?.name);
    const where = row.otherParentKey ? (parentName ? `under ${parentName}` : 'under another parent') : 'at the root';
    const canReveal = !row.otherParentKey || parentRow !== undefined;

    return html`
      ${icon}
      <span class="placeholder">${side === 'local' ? 'Lives' : `Lives on ${remoteName}`} ${where}</span>
      ${
        canReveal
          ? html`<uui-button
              look="secondary"
              compact
              label=${`Show ${row.local?.name ?? row.remote?.name ?? ''} ${where}`}
              @click=${() => this.#reveal(row.otherParentKey, row.key)}
              ><span class="chip"><umb-icon name="icon-locate"></umb-icon>Show</span></uui-button
            >`
          : nothing
      }
    `;
  }

  static override styles = [
    css`
      :host {
        --transfr-step: var(--uui-size-space-6);
        --transfr-row-height: var(--uui-size-11);
        display: block;
        padding: var(--uui-size-layout-1);
      }

      ul {
        list-style: none;
        margin: 0;
        padding: 0;
      }

      .toolbar {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-5);
      }

      .legend {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2) var(--uui-size-space-5);
        margin-left: auto;
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

      .legend li {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .swatch {
        display: inline-block;
        width: 1.5em;
        height: 1em;
        border-radius: var(--uui-border-radius);
        border: 1px solid var(--uui-color-border);
        background: var(--uui-color-surface);
      }

      .panes {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
        column-gap: var(--uui-size-space-5);
      }

      .tree {
        grid-column: 1 / -1;
      }

      .pane-head {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-4) var(--uui-size-space-5);
        border-radius: var(--uui-border-radius) var(--uui-border-radius) 0 0;
        border-top: 4px solid var(--uui-color-interactive);
        background: var(--uui-color-surface-alt);
        margin-bottom: var(--uui-size-space-2);
      }

      .pane-head.remote {
        border-top-color: var(--uui-color-current);
      }

      .pane-head umb-icon {
        font-size: 1.5em;
      }

      .pane-head div {
        display: flex;
        flex-direction: column;
        min-width: 0;
      }

      .pane-head small {
        opacity: 0.8;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      /* One row spans both trees, so the two sides always line up. */
      .row {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
        column-gap: var(--uui-size-space-5);
      }

      .cell {
        position: relative;
        isolation: isolate;
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        min-width: 0;
        min-height: var(--transfr-row-height);
        padding-left: calc(var(--depth, 0) * var(--transfr-step) + var(--uui-size-space-3));
        padding-right: var(--uui-size-space-3);
        background-color: var(--uui-color-surface);
        border-left: 4px solid transparent;
        border-bottom: 1px solid var(--uui-color-divider);
        transition: background-color 120ms;
      }

      /* Indent guides, one line per ancestor level. */
      .cell::before {
        content: '';
        position: absolute;
        inset: 0 auto 0 calc(var(--uui-size-space-3) + var(--transfr-step) / 2);
        width: calc(var(--depth, 0) * var(--transfr-step));
        background-image: repeating-linear-gradient(
          to right,
          var(--uui-color-divider-standalone) 0 1px,
          transparent 1px var(--transfr-step)
        );
        pointer-events: none;
      }

      /* Hover darkens whatever the cell shows (white, green, amber, hatched) instead of replacing it, so a coloured
         row keeps its meaning. The overlay sits under the content: .cell isolates, the overlay has a negative z-index. */
      .cell::after {
        content: '';
        position: absolute;
        inset: 0;
        z-index: -1;
        pointer-events: none;
        transition: background-color 120ms;
      }

      .row:hover .cell::after,
      .row:has(:focus-visible) .cell::after {
        background-color: color-mix(in srgb, var(--uui-color-text) 7%, transparent);
      }

      .expander {
        flex-shrink: 0;
        width: var(--uui-size-8);
      }

      .doc {
        flex-shrink: 0;
        font-size: 1.15em;
        color: var(--uui-color-text-alt);
      }

      .doc.faded {
        opacity: 0.35;
      }

      .name {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .chips {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
        margin-left: auto;
      }

      /* Block-level flex: an inline-flex box sits on the text baseline and leaves descender space below it, which put
         the content 1.3 px above the middle of the tag (measured). */
      .chip {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-1);
        white-space: nowrap;
      }

      /* Only on this side: a solid accent and a tint. */
      .cell.only,
      .swatch.only {
        border-left-color: var(--uui-color-positive);
        background-color: color-mix(in srgb, var(--uui-color-positive) 12%, var(--uui-color-surface));
      }

      .cell.only .doc {
        color: var(--uui-color-positive-standalone);
      }

      /* Under another parent on the other side: a warning accent. */
      .cell.moved,
      .swatch.moved {
        border-left-color: var(--uui-color-warning);
        background-color: color-mix(in srgb, var(--uui-color-warning) 16%, var(--uui-color-surface));
      }

      /* Not on this side (or not in this place): a hatched, dashed placeholder. */
      .cell.missing,
      .cell.elsewhere,
      .swatch.missing {
        background-image: repeating-linear-gradient(
          -45deg,
          transparent 0 6px,
          color-mix(in srgb, var(--uui-color-border) 45%, transparent) 6px 7px
        );
        outline: 1px dashed var(--uui-color-border-standalone);
        outline-offset: -4px;
      }

      .cell.elsewhere {
        outline-color: var(--uui-color-warning-standalone);
      }

      .placeholder {
        color: var(--uui-color-text-alt);
        font-style: italic;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .swatch.conflict {
        border-left: 4px solid var(--uui-color-danger);
      }

      /* Hovering a node lights up its namesake with another key in the other tree. */
      .row.paired .cell:not(.missing) {
        box-shadow: inset 0 0 0 2px var(--uui-color-danger);
      }

      .row.flash .cell {
        box-shadow: inset 0 0 0 2px var(--uui-color-focus, var(--uui-color-interactive-emphasis));
      }

      @media (prefers-reduced-motion: no-preference) {
        .row.flash .cell {
          animation: transfr-flash 2s ease-out;
        }
      }

      @keyframes transfr-flash {
        from {
          background-color: color-mix(in srgb, var(--uui-color-interactive-emphasis) 25%, var(--uui-color-surface));
        }
      }

      .message {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        min-height: var(--transfr-row-height);
        padding-left: calc(var(--depth, 0) * var(--transfr-step) + var(--uui-size-space-3) + var(--uui-size-8));
        background: var(--uui-color-surface);
        border-bottom: 1px solid var(--uui-color-divider);
      }

      .message uui-loader-bar {
        flex: 1;
        max-width: 240px;
      }

      .empty {
        color: var(--uui-color-text-alt);
        font-style: italic;
      }

      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default TransfrContentCompareElement;

declare global {
  interface HTMLElementTagNameMap {
    'transfr-content-compare': TransfrContentCompareElement;
  }
}
