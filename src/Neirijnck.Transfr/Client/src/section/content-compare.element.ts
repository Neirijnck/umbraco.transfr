import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { TransfrService } from '../api/index.js';
import type { RemoteResponseModel, RemoteStatusResponseModel } from '../api/index.js';

/**
 * Content compare view. For now: the remotes, a connection test per remote, and connecting a remote at runtime.
 */
@customElement('transfr-content-compare')
export class TransfrContentCompareElement extends UmbLitElement {
  @state()
  private _remotes: Array<RemoteResponseModel> = [];

  @state()
  private _allowRuntimeRemotes = false;

  @state()
  private _status: Record<string, RemoteStatusResponseModel | 'testing'> = {};

  @state()
  private _connecting = false;

  @state()
  private _connectError?: string;

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    const { data } = await tryExecute(this, TransfrService.getRemotes());
    this._remotes = data?.remotes ?? [];
    this._allowRuntimeRemotes = data?.allowRuntimeRemotes ?? false;
  }

  async #test(alias: string) {
    this._status = { ...this._status, [alias]: 'testing' };
    const { data } = await tryExecute(this, TransfrService.getStatus({ path: { alias } }));
    if (data) {
      this._status = { ...this._status, [alias]: data };
    }
  }

  async #connect(event: SubmitEvent) {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    const field = (name: string) => String(new FormData(form).get(name) ?? '').trim();

    this._connecting = true;
    this._connectError = undefined;
    const { data, error } = await tryExecute(
      this,
      TransfrService.connect({
        body: {
          name: field('name'),
          baseUrl: field('baseUrl'),
          clientId: field('clientId'),
          clientSecret: field('clientSecret'),
        },
      }),
      { disableNotifications: true },
    );
    this._connecting = false;

    if (!data) {
      const problem = (error as { problemDetails?: { title?: string; detail?: string } } | undefined)?.problemDetails;
      this._connectError = [problem?.title, problem?.detail].filter(Boolean).join(': ') || 'Connecting failed.';
      return;
    }

    form.reset();
    await this.#load();
    await this.#test(data.alias);
  }

  async #disconnect(alias: string) {
    await tryExecute(this, TransfrService.disconnect({ path: { alias } }));
    const { [alias]: _, ...rest } = this._status;
    this._status = rest;
    await this.#load();
  }

  override render() {
    return html`
      <uui-box headline="Remotes">
        ${
          this._remotes.length === 0
            ? html`<p>No remotes yet.${this._allowRuntimeRemotes ? ' Connect one below.' : ''}</p>`
            : this.#renderTable()
        }
      </uui-box>
      ${this._allowRuntimeRemotes ? this.#renderConnectForm() : nothing}
    `;
  }

  #renderTable() {
    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>Name</uui-table-head-cell>
          <uui-table-head-cell>Address</uui-table-head-cell>
          <uui-table-head-cell>Connection</uui-table-head-cell>
          <uui-table-head-cell></uui-table-head-cell>
        </uui-table-head>
        ${repeat(
          this._remotes,
          (remote) => remote.alias,
          (remote) => html`
            <uui-table-row>
              <uui-table-cell>
                ${remote.name} <small>(${remote.alias})</small>
                <uui-tag look="secondary">${remote.source === 'runtime' ? 'Connected now' : 'Configured'}</uui-tag>
              </uui-table-cell>
              <uui-table-cell>${remote.baseUrl}</uui-table-cell>
              <uui-table-cell>${this.#renderConnection(remote)}</uui-table-cell>
              <uui-table-cell>
                ${
                  remote.source === 'runtime'
                    ? html`<uui-button
                        look="secondary"
                        color="danger"
                        label="Disconnect"
                        @click=${() => this.#disconnect(remote.alias)}
                      ></uui-button>`
                    : nothing
                }
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
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

  #renderConnectForm() {
    return html`
      <uui-box headline="Connect a remote">
        <p>
          Signs in with the remote's API user. The connection is kept in memory for you only, until you disconnect or
          the site restarts.
        </p>
        <form @submit=${this.#connect}>
          <uui-form-layout-item>
            <uui-label slot="label" for="name" required>Name</uui-label>
            <uui-input id="name" name="name" required label="Name" placeholder="Staging"></uui-input>
          </uui-form-layout-item>
          <uui-form-layout-item>
            <uui-label slot="label" for="baseUrl" required>Address</uui-label>
            <uui-input
              id="baseUrl"
              name="baseUrl"
              type="url"
              required
              label="Address"
              placeholder="https://staging.example.com"
            ></uui-input>
          </uui-form-layout-item>
          <uui-form-layout-item>
            <uui-label slot="label" for="clientId" required>Client id</uui-label>
            <uui-input
              id="clientId"
              name="clientId"
              required
              label="Client id"
              placeholder="umbraco-back-office-transfr"
            ></uui-input>
          </uui-form-layout-item>
          <uui-form-layout-item>
            <uui-label slot="label" for="clientSecret" required>Client secret</uui-label>
            <uui-input
              id="clientSecret"
              name="clientSecret"
              type="password"
              required
              label="Client secret"
              autocomplete="off"
            ></uui-input>
          </uui-form-layout-item>
          ${this._connectError ? html`<p class="error" data-test="connect-error">${this._connectError}</p>` : nothing}
          <uui-button
            type="submit"
            look="primary"
            label="Connect"
            .state=${this._connecting ? 'waiting' : undefined}
          ></uui-button>
        </form>
      </uui-box>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      uui-box + uui-box {
        margin-top: var(--uui-size-layout-1);
      }

      uui-tag {
        margin-inline: var(--uui-size-space-2);
      }

      uui-input {
        width: 100%;
        max-width: 480px;
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
