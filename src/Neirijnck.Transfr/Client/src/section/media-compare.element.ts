import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { TransfrContentCompareElement } from './content-compare.element.js';

/**
 * Media compare view: this environment's media tree next to a remote's, compared like the content tree (by key, one
 * level at a time, with the same marks). Media can't be picked for a copy or opened in a diff here yet.
 */
@customElement('transfr-media-compare')
export class TransfrMediaCompareElement extends TransfrContentCompareElement {
  protected override readonly kind = 'Media';
}

export default TransfrMediaCompareElement;

declare global {
  interface HTMLElementTagNameMap {
    'transfr-media-compare': TransfrMediaCompareElement;
  }
}
