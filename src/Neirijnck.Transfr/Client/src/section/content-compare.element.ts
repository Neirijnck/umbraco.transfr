import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type { RemoteResponseModel, RemoteStatusResponseModel } from '../api/index.js';

/**
 * Content compare view. For now: the configured remotes and a connection test per remote.
 */
@customElement('transfr-content-compare')
export class TransfrContentCompareElement extends UmbLitElement {
  @state()
  private _remotes: Array<RemoteResponseModel> = [];

  @state()
  private _status: Record<string, RemoteStatusResponseModel | 'testing'> = {};

  override async connectedCallback() {
    super.connectedCallback();
    const { data } = await tryExecute(this, TransfrService.getRemotes());
    this._remotes = data ?? [];
  }

  async #test(alias: string) {
    this._status = { ...this._status, [alias]: 'testing' };
    const { data } = await tryExecute(this, TransfrService.getStatus({ path: { alias } }));
    if (data) {
      this._status = { ...this._status, [alias]: data };
    }
  }

  override render() {
    return html`
      <uui-box headline="Remotes">
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>Name</uui-table-head-cell>
            <uui-table-head-cell>Address</uui-table-head-cell>
            <uui-table-head-cell>Connection</uui-table-head-cell>
          </uui-table-head>
          ${repeat(
            this._remotes,
            (remote) => remote.alias,
            (remote) => html`
              <uui-table-row>
                <uui-table-cell>${remote.name} <small>(${remote.alias})</small></uui-table-cell>
                <uui-table-cell>${remote.baseUrl}</uui-table-cell>
                <uui-table-cell>${this.#renderConnection(remote)}</uui-table-cell>
              </uui-table-row>
            `,
          )}
        </uui-table>
      </uui-box>
    `;
  }

  #renderConnection(remote: RemoteResponseModel) {
    if (!remote.isValid) {
      return html`<uui-tag color="danger">Invalid configuration</uui-tag> ${remote.errors.join(' ')}`;
    }

    const status = this._status[remote.alias];
    return html`
      <uui-button
        look="secondary"
        label="Test connection"
        .state=${status === 'testing' ? 'waiting' : undefined}
        @click=${() => this.#test(remote.alias)}
      ></uui-button>
      ${status && status !== 'testing' ? this.#renderStatus(status) : nothing}
    `;
  }

  #renderStatus(status: RemoteStatusResponseModel) {
    if (status.error) {
      return html`<uui-tag color="danger">Failed</uui-tag> <span data-test="status">${status.error}</span>`;
    }

    return html`<uui-tag color="positive">Connected</uui-tag>
      <span data-test="status">${status.serverStatus}, ${status.rootDocuments} root document(s)</span>`;
  }

  static override styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      uui-tag {
        margin-inline: var(--uui-size-space-2);
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
