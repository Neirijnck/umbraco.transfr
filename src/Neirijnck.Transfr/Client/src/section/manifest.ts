import { TRANSFR_COPY_PLAN_MODAL } from './copy-plan-modal.token.js';
import { TRANSFR_NODE_DIFF_MODAL } from './node-diff-modal.token.js';

export const TRANSFR_SECTION_ALIAS = 'Transfr.Section';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'section',
    alias: TRANSFR_SECTION_ALIAS,
    name: 'Transfr Section',
    // Sections show by weight, highest first (Umbraco's own run from 1000 down to 400). A tool section goes after the
    // site's own sections, which take positive weights.
    weight: -100,
    meta: {
      label: 'Transfr',
      pathname: 'transfr',
    },
    conditions: [{ alias: 'Umb.Condition.SectionUserPermission', match: TRANSFR_SECTION_ALIAS }],
  },
  {
    type: 'sectionView',
    alias: 'Transfr.SectionView.Content',
    name: 'Transfr Content Compare',
    // The higher weight comes first: comparing is the main view.
    weight: 200,
    element: () => import('./content-compare.element.js'),
    meta: {
      label: 'Content',
      pathname: 'content',
      icon: 'icon-split',
    },
    conditions: [{ alias: 'Umb.Condition.SectionAlias', match: TRANSFR_SECTION_ALIAS }],
  },
  {
    type: 'sectionView',
    alias: 'Transfr.SectionView.Media',
    name: 'Transfr Media Compare',
    weight: 175,
    element: () => import('./media-compare.element.js'),
    meta: {
      label: 'Media',
      pathname: 'media',
      icon: 'icon-pictures',
    },
    conditions: [{ alias: 'Umb.Condition.SectionAlias', match: TRANSFR_SECTION_ALIAS }],
  },
  {
    type: 'sectionView',
    alias: 'Transfr.SectionView.History',
    name: 'Transfr History',
    weight: 150,
    element: () => import('./history.element.js'),
    meta: {
      label: 'History',
      pathname: 'history',
      icon: 'icon-history',
    },
    conditions: [{ alias: 'Umb.Condition.SectionAlias', match: TRANSFR_SECTION_ALIAS }],
  },
  {
    type: 'sectionView',
    alias: 'Transfr.SectionView.Remotes',
    name: 'Transfr Remotes',
    weight: 100,
    element: () => import('./remotes.element.js'),
    meta: {
      label: 'Remotes',
      pathname: 'remotes',
      icon: 'icon-server',
    },
    conditions: [{ alias: 'Umb.Condition.SectionAlias', match: TRANSFR_SECTION_ALIAS }],
  },
  {
    type: 'modal',
    alias: TRANSFR_NODE_DIFF_MODAL.toString(),
    name: 'Transfr Node Diff',
    element: () => import('./node-diff-modal.element.js'),
  },
  {
    type: 'modal',
    alias: TRANSFR_COPY_PLAN_MODAL.toString(),
    name: 'Transfr Copy Plan',
    element: () => import('./copy-plan-modal.element.js'),
  },
];
