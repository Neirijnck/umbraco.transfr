import { css, customElement, html, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { TransfrService } from '../api/index.js';

/**
 * Content compare view. Scaffold: shows that the section loads and the API answers.
 */
@customElement('transfr-content-compare')
export class TransfrContentCompareElement extends UmbLitElement {
  @state()
  private _ping = '…';

  override async connectedCallback() {
    super.connectedCallback();
    const { data, error } = await TransfrService.ping();
    this._ping = error ? `error: ${JSON.stringify(error)}` : String(data);
  }

  override render() {
    return html`
      <uui-box headline="Transfr">
        <p>Content compare comes here.</p>
        <p>API ping: <strong data-test="ping">${this._ping}</strong></p>
      </uui-box>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
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
