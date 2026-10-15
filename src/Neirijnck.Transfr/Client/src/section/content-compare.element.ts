import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUISelectElement } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type { RemoteResponseModel, SeparateInstallResponseModel, TreeRowResponseModel } from '../api/index.js';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { describeDifference, summarizeDifferences } from './content-summary.js';
import { TRANSFR_COPY_PLAN_MODAL } from './copy-plan-modal.token.js';
import { TRANSFR_NODE_DIFF_MODAL } from './node-diff-modal.token.js';
import { problemMessage } from './problem.js';
import { blockOf, describeSeparateInstall, passesBlockOn } from './schema-view.js';
import type { RowBlock } from './schema-view.js';
import {
  coverForChildren,
  describeSelection,
  fromRequest,
  setIncludeDescendants,
  toRequest,
  toggleSelected,
  whyNotSelectable,
} from './selection.js';
import type { Selection } from './selection.js';
import { countSubtree, describeCounts, formatDuration, positionsToReveal } from './subtree.js';
import type { SubtreeCounts } from './subtree.js';
import { rowView } from './tree-row-view.js';
import type { SideKind } from './tree-row-view.js';

type Level =
  { state: 'loading' } | { state: 'failed'; message: string } | { state: 'loaded'; rows: Array<TreeRowResponseModel> };

type Side = 'local' | 'remote';

type SubtreeRun =
  | { state: 'running'; label: string }
  | { state: 'failed'; label: string; message: string }
  | { state: 'done'; label: string; counts: SubtreeCounts; truncated: boolean; milliseconds: number };

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

  // The last "compare subtree": running, its outcome, or why it failed.
  @state()
  private _subtree?: SubtreeRun;

  // Evidence that the remote was installed separately: then nothing can be copied to or from it.
  @state()
  private _separateInstall: Array<SeparateInstallResponseModel> = [];

  // The nodes picked for a copy to the current remote, by key (a moved node is picked once, in its place here).
  @state()
  private _selection: Selection = new Map();

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
    this._subtree = undefined;
    this._separateInstall = [];
    this._selection = new Map();
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
    this.#addSeparateInstall(data?.separateInstall ?? []);
  }

  // Evidence of a separately installed remote, from any level or subtree compared so far.
  #addSeparateInstall(evidence: Array<SeparateInstallResponseModel>) {
    const known = new Set(this._separateInstall.map((item) => item.localKey));
    const added = evidence.filter((item) => !known.has(item.localKey));
    if (added.length > 0) {
      this._separateInstall = [...this._separateInstall, ...added];
    }
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

  // Compares every level below a parent in one go, puts the levels in the tree and opens it down to each difference.
  async #compareSubtree(parentKey: string, position: string | undefined, label: string) {
    const alias = this._alias;
    if (!alias) {
      return;
    }

    this._subtree = { state: 'running', label };
    const started = performance.now();
    const { data, error } = await tryExecute(
      this,
      TransfrService.compareSubtree({ path: { alias }, query: parentKey === ROOT ? undefined : { parentKey } }),
      { disableNotifications: true },
    );

    // The remote was switched while comparing: the result belongs to the old tree.
    if (alias !== this._alias) {
      return;
    }

    if (!data) {
      this._subtree = { state: 'failed', label, message: problemMessage(error, 'Comparing failed.') };
      return;
    }

    const levels = { ...this._levels };
    for (const level of data.levels) {
      levels[level.parentKey ?? ROOT] = { state: 'loaded', rows: level.rows };
    }
    this._levels = levels;
    this.#addSeparateInstall(data.separateInstall);

    const expanded = new Set([...this._expanded, ...positionsToReveal(data.levels)]);
    if (position) {
      expanded.add(position);
    }
    this._expanded = expanded;

    this._subtree = {
      state: 'done',
      label,
      counts: countSubtree(data.levels),
      truncated: data.truncated,
      milliseconds: performance.now() - started,
    };
  }

  // A click anywhere on a row of a node on both sides opens its diff, except on the row's own controls (expand, select,
  // Show, compare below), which do their own thing. The name stays a button so the diff is reachable by keyboard too.
  #rowClick(event: MouseEvent, row: TreeRowResponseModel) {
    if (!row.content) {
      return;
    }

    const onControl = event
      .composedPath()
      .some(
        (target) => target instanceof HTMLElement && ['BUTTON', 'UUI-BUTTON', 'INPUT', 'A'].includes(target.tagName),
      );
    if (!onControl) {
      this.#openDiff(row);
    }
  }

  // A node found on both sides opens its diff; closing the panel rejects the promise, which is not an error.
  #openDiff(row: TreeRowResponseModel) {
    const alias = this._alias;
    if (!alias) {
      return;
    }

    const remote = this._remotes?.find((candidate) => candidate.alias === alias);
    umbOpenModal(this, TRANSFR_NODE_DIFF_MODAL, {
      data: {
        alias,
        remoteName: remote?.name ?? alias,
        key: row.key,
        name: row.local?.name ?? row.remote?.name ?? row.key,
      },
    }).catch(() => undefined);
  }

  // The dry run of copying the selection; it may add nodes to the selection, which come back when it closes. Closing
  // it any other way (Escape) rejects the promise and keeps the selection as it was, which is not an error.
  #openPlan() {
    const alias = this._alias;
    if (!alias || this._selection.size === 0) {
      return;
    }

    const remote = this._remotes?.find((candidate) => candidate.alias === alias);
    umbOpenModal(this, TRANSFR_COPY_PLAN_MODAL, {
      data: { alias, remoteName: remote?.name ?? alias, selection: toRequest(this._selection) },
    })
      .then((value) => {
        if (alias === this._alias) {
          this._selection = fromRequest(value.selection);
        }
      })
      .catch(() => undefined);
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
        <uui-button
          look="secondary"
          label="Compare everything"
          .state=${this._subtree?.state === 'running' ? 'waiting' : undefined}
          ?disabled=${this._subtree?.state === 'running'}
          @click=${() => this.#compareSubtree(ROOT, undefined, 'the whole tree')}
        >
          <umb-icon name="icon-tree"></umb-icon> Compare everything
        </uui-button>
        ${this.#renderLegend()}
      </div>
      ${this.#renderSelectionBar(remote?.name ?? 'the remote')}
      ${
        this._separateInstall.length > 0
          ? html`<div class="banner error" role="alert" data-test="separate-install">
              <umb-icon name="icon-block"></umb-icon>
              <span>${describeSeparateInstall(this._separateInstall, remote?.name ?? 'The remote')}</span>
            </div>`
          : nothing
      }
      ${this.#renderSubtree(remote?.name ?? 'the remote')}
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
        <div class="tree ${this._separateInstall.length > 0 ? 'refused' : ''}">
          ${this.#renderLevel(ROOT, 0, remote?.name ?? 'the remote', undefined, undefined)}
        </div>
      </div>
    `;
  }

  // What is picked for a copy, and the way to its dry run. Hidden until something is picked.
  #renderSelectionBar(remoteName: string) {
    if (this._selection.size === 0) {
      return nothing;
    }

    return html`<div class="banner selection" role="status" data-test="selection">
      <umb-icon name="icon-checkbox"></umb-icon>
      <span>${describeSelection(this._selection)}.</span>
      <uui-button look="secondary" label="Clear selection" @click=${() => (this._selection = new Map())}></uui-button>
      <uui-button
        look="primary"
        label=${`Dry run: copy to ${remoteName}`}
        ?disabled=${this._separateInstall.length > 0}
        @click=${() => this.#openPlan()}
        ><umb-icon name="icon-search"></umb-icon> Dry run: copy to ${remoteName}</uui-button
      >
    </div>`;
  }

  #renderSubtree(remoteName: string) {
    const run = this._subtree;
    if (!run) {
      return nothing;
    }

    const close = html`<uui-button
      compact
      label="Close"
      ?disabled=${run.state === 'running'}
      @click=${() => (this._subtree = undefined)}
      ><umb-icon name="icon-wrong"></umb-icon
    ></uui-button>`;

    if (run.state === 'running') {
      return html`<div class="banner" role="status">
        <uui-loader-circle></uui-loader-circle>
        <span>Comparing ${run.label} with ${remoteName}…</span>
      </div>`;
    }

    if (run.state === 'failed') {
      return html`<div class="banner error" role="alert" data-test="subtree-error">
        <umb-icon name="icon-alert"></umb-icon>
        <span>Comparing ${run.label} failed: ${run.message}</span>
        ${close}
      </div>`;
    }

    return html`<div class="banner" role="status" data-test="subtree-summary">
      <umb-icon name="icon-tree"></umb-icon>
      <span>
        Compared ${run.label} in ${formatDuration(run.milliseconds)}. ${describeCounts(run.counts, remoteName)}
        ${run.truncated ? 'Stopped at the node limit: compare a smaller branch for the rest.' : nothing}
      </span>
      ${close}
    </div>`;
  }

  #renderLegend() {
    return html`
      <ul class="legend" aria-label="Legend">
        <li><span class="swatch same"></span> Same place in both</li>
        <li><span class="swatch changed"></span> Content differs</li>
        <li><span class="swatch only"></span> Only on this side</li>
        <li><span class="swatch missing"></span> Not on this side</li>
        <li><span class="swatch moved"></span> Under another parent on the other side</li>
        <li><span class="swatch conflict"></span> Same name, other key</li>
        <li><span class="swatch blocked"></span> Blocked by the schema</li>
      </ul>
    `;
  }

  // blockedParent: the name of a parent that can't be copied and isn't on the other side, so this level inherits it.
  // coveredBy: the name of a picked ancestor that brings this level along (with everything below it).
  #renderLevel(
    parentKey: string,
    depth: number,
    remoteName: string,
    blockedParent: string | undefined,
    coveredBy: string | undefined,
  ): unknown {
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
        (row) => this.#renderRow(parentKey, row, depth, remoteName, blockedParent, coveredBy),
      )}
    </ul>`;
  }

  #renderRow(
    parentKey: string,
    row: TreeRowResponseModel,
    depth: number,
    remoteName: string,
    blockedParent: string | undefined,
    coveredBy: string | undefined,
  ) {
    const position = `${parentKey}/${row.key}`;
    const expanded = this._expanded.has(position);
    const view = rowView(row.status);
    const paired = this._hoverKey !== undefined && row.sameNameKey === this._hoverKey;
    const block = blockOf(row, blockedParent, remoteName);
    const name = row.local?.name ?? row.remote?.name ?? row.key;
    const classes = [
      'row',
      row.content ? 'opens-diff' : '',
      block ? 'blocked' : '',
      paired ? 'paired' : '',
      this._flash === position ? 'flash' : '',
    ].join(' ');

    return html`
      <li data-key=${row.key} data-status=${row.status} data-position=${position}>
        <div
          class=${classes}
          style="--depth: ${depth}"
          @mouseenter=${() => (this._hoverKey = row.key)}
          @mouseleave=${() => (this._hoverKey = undefined)}
          @focusin=${() => (this._hoverKey = row.key)}
          @focusout=${() => (this._hoverKey = undefined)}
          @click=${(event: MouseEvent) => this.#rowClick(event, row)}
        >
          ${this.#renderCell('local', parentKey, row, view.local, view.known, expanded, remoteName, block, coveredBy)}
          ${this.#renderCell('remote', parentKey, row, view.remote, view.known, expanded, remoteName, block, coveredBy)}
        </div>
        ${
          expanded
            ? this.#renderLevel(
                row.key,
                depth + 1,
                remoteName,
                block && passesBlockOn(row) ? name : undefined,
                coverForChildren(this._selection, row.key, name, coveredBy),
              )
            : nothing
        }
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
    block: RowBlock | undefined,
    coveredBy: string | undefined,
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

    // Changed content tints both sides of a row that is in the same place; a structural difference keeps its own colour.
    const changed = kind === 'same' && row.content?.status === 'Different';

    return html`
      <div class="cell ${side} ${kind} ${changed ? 'changed' : ''}">
        ${expander} ${side === 'local' ? this.#renderPick(row, kind, name, block, coveredBy) : nothing}
        ${
          node && kind !== 'missing' && kind !== 'elsewhere'
            ? html`
                <umb-icon
                  class="doc"
                  name=${node.documentTypeIcon ?? 'icon-document'}
                  title=${node.documentTypeName ?? 'Document type unknown here'}
                ></umb-icon>
                ${
                  row.content
                    ? html`<button
                        class="name link"
                        title=${`Show ${node.name} on both sides`}
                        @click=${() => this.#openDiff(row)}
                      >
                        ${node.name}
                      </button>`
                    : html`<span class="name">${node.name}</span>`
                }
                ${side === 'local' && node.hasChildren && !coveredBy ? this.#renderBelow(row.key, name) : nothing}
                <span class="chips">${this.#renderChips(side, row, kind, known, remoteName, block)}</span>
              `
            : this.#renderPlaceholder(side, row, kind, remoteName)
        }
        ${
          side === 'local' && hasChildren
            ? html`<uui-button
                class="subtree"
                compact
                look="secondary"
                label=${`Compare everything below ${name}`}
                title=${`Compare everything below ${name}`}
                ?disabled=${this._subtree?.state === 'running'}
                @click=${() => this.#compareSubtree(row.key, `${parentKey}/${row.key}`, `everything below ${name}`)}
                ><umb-icon name="icon-tree"></umb-icon
              ></uui-button>`
            : nothing
        }
      </div>
    `;
  }

  // A checkbox where the node is here in this place; a disabled one says why it can't be picked. A node a picked
  // ancestor brings along is shown ticked and disabled: it goes along, and is left out by changing that ancestor's
  // choice. A placeholder for a node that isn't here (or lives elsewhere here) gets an empty slot, so names stay in line.
  #renderPick(
    row: TreeRowResponseModel,
    kind: SideKind,
    name: string,
    block: RowBlock | undefined,
    coveredBy: string | undefined,
  ) {
    if (kind === 'missing' || kind === 'elsewhere') {
      return html`<span class="pick"></span>`;
    }

    const why = whyNotSelectable(row, block, this._separateInstall.length > 0);
    const along =
      why === undefined && coveredBy !== undefined ? `Comes along with everything below ${coveredBy}` : undefined;
    const title = why ?? along ?? `Select ${name} for a copy`;
    // A native checkbox: uui-checkbox always shows its label next to the box (no way to keep it for screen readers
    // only), which doesn't fit a tree row.
    return html`<input
      type="checkbox"
      class="pick"
      aria-label=${why ? `${name} can't be selected: ${why}` : along ? `${name}: ${along}` : title}
      title=${title}
      .checked=${along !== undefined || (why === undefined && this._selection.has(row.key))}
      ?disabled=${why !== undefined || along !== undefined}
      @change=${() => (this._selection = toggleSelected(this._selection, row.key))}
    />`;
  }

  // For a picked node with children: whether everything below it comes along. Shown only once it is picked.
  #renderBelow(key: string, name: string) {
    if (!this._selection.has(key)) {
      return nothing;
    }

    const include = this._selection.get(key) === true;
    return html`<uui-button
      class="below"
      compact
      look=${include ? 'primary' : 'outline'}
      aria-pressed=${include ? 'true' : 'false'}
      label=${`Include everything below ${name}`}
      title=${include ? 'Everything below this node comes along. Click for this node only.' : 'Only this node. Click to bring everything below it along.'}
      @click=${() => (this._selection = setIncludeDescendants(this._selection, key, !include))}
      ><span class="chip"
        ><umb-icon name="icon-tree"></umb-icon>${include ? 'With everything below' : 'This node only'}</span
      ></uui-button
    >`;
  }

  // Chips only for differences: identical nodes stay quiet.
  #renderChips(
    side: Side,
    row: TreeRowResponseModel,
    kind: SideKind,
    known: boolean,
    remoteName: string,
    block: RowBlock | undefined,
  ) {
    // Content and order belong to the pair: told once, on the left for a row in the same place, else where it is.
    const tellsContent = (kind === 'same' && side === 'local') || kind === 'moved';
    // A block is told once too, on the side the node would be copied from.
    const tellsBlock = block && (tellsContent || kind === 'only');
    return html`
      ${
        tellsBlock
          ? html`<uui-tag class="blocked-chip" color="danger" look="outline" title=${block.details.join('\n')}
              ><span class="chip"
                ><umb-icon name="icon-block"></umb-icon>${block.inherited ? 'Blocked by parent' : 'Blocked'}</span
              ></uui-tag
            >`
          : nothing
      }
      ${tellsContent ? this.#renderContentChips(row, remoteName) : nothing}
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

  #renderContentChips(row: TreeRowResponseModel, remoteName: string) {
    const content = row.content;
    return html`
      ${
        content?.status === 'Different'
          ? html`<span class="summary">${summarizeDifferences(content.differences)}</span> ${this.#chip(
                'default',
                'icon-edit',
                'Changed',
                `Differs from ${remoteName}:\n${content.differences.map(describeDifference).join('\n')}`,
              )}`
          : nothing
      }
      ${
        content?.status === 'Failed'
          ? this.#chip(
              'danger',
              'icon-wrong',
              'Not compared',
              content.error ?? 'The content could not be compared.',
              'outline',
            )
          : nothing
      }
      ${
        row.orderDiffers
          ? this.#chip(
              'default',
              'icon-navigation-vertical',
              'Order differs',
              `Sits in another place among its siblings on ${remoteName}.`,
              'outline',
            )
          : nothing
      }
    `;
  }

  #chip(
    color: 'default' | 'positive' | 'warning' | 'danger',
    icon: string,
    text: string,
    title?: string,
    look: 'primary' | 'outline' = 'primary',
  ) {
    return html`<uui-tag color=${color} look=${look} title=${title ?? text}
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

      .banner {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-5);
        padding: var(--uui-size-space-3) var(--uui-size-space-5);
        border-left: 4px solid var(--uui-color-selected);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      .banner > span {
        flex: 1;
      }

      .banner.error {
        border-left-color: var(--uui-color-danger);
        color: var(--uui-color-danger);
      }

      /* Shown on hover and on keyboard focus, so the trees stay calm but the button stays reachable. */
      .subtree {
        flex-shrink: 0;
        opacity: 0;
      }

      .row:hover .subtree,
      .subtree:focus-within {
        opacity: 1;
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

      .pick {
        flex-shrink: 0;
        width: var(--uui-size-5);
        height: var(--uui-size-5);
        margin: 0;
        accent-color: var(--uui-color-selected);
        cursor: pointer;
      }

      .pick:disabled {
        cursor: not-allowed;
      }

      .pick:focus-visible {
        outline: 2px solid var(--uui-color-focus);
        outline-offset: 2px;
      }

      .below {
        flex-shrink: 0;
      }

      .banner.selection {
        border-left-color: var(--uui-color-focus, var(--uui-color-selected));
      }

      .doc {
        flex-shrink: 0;
        font-size: 1.15em;
        color: var(--uui-color-text-alt);
      }

      .doc.faded {
        opacity: 0.35;
      }

      .row.opens-diff {
        cursor: pointer;
      }

      /* Can't be copied (schema, or a parent that can't): greyed out, colours muted, only the Blocked chip stands out.
         The reason is in the chip's tooltip, so it's never conveyed by grey alone. A refused remote greys everything. */
      .row.blocked .cell,
      .tree.refused .cell,
      .swatch.blocked {
        background-color: var(--uui-color-disabled);
        border-left-color: var(--uui-color-disabled-standalone);
      }

      /* The hover-only subtree button keeps its own opacity; the Blocked chip keeps its colour; the expander stays
         sharp, because a blocked node can still be opened (greyed, it looked disabled: Preben couldn't open one). */
      .row.blocked .cell > :not(.chips):not(.subtree):not(.expander),
      .row.blocked uui-tag:not(.blocked-chip),
      .tree.refused .cell > :not(.chips):not(.subtree):not(.expander),
      .tree.refused uui-tag:not(.blocked-chip) {
        opacity: 0.55;
        filter: grayscale(1);
      }

      /* The name of a node on both sides opens its diff: a button that reads as the name. */
      .name.link {
        padding: 0;
        border: 0;
        background: none;
        color: inherit;
        font: inherit;
        text-align: left;
        cursor: pointer;
      }

      .name.link:hover {
        text-decoration: underline;
      }

      .name.link:focus-visible {
        outline: 2px solid var(--uui-color-focus);
        outline-offset: 2px;
      }

      .name {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .chips {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2);
        margin-left: auto;
        min-width: 0;
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
      /* Same place in both, other content: a blue accent and tint on both sides. "Selected" is blue in the light and the
         dark theme; "interactive" is near-white in the dark theme. */
      .cell.changed,
      .swatch.changed {
        border-left-color: var(--uui-color-selected);
        background-color: color-mix(in srgb, var(--uui-color-selected) 12%, var(--uui-color-surface));
      }

      .summary {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }

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
