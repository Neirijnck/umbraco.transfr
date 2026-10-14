export const TRANSFR_SECTION_ALIAS = 'Transfr.Section';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'section',
    alias: TRANSFR_SECTION_ALIAS,
    name: 'Transfr Section',
    weight: 10,
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
];
