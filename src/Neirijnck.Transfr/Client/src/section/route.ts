import { css, html } from '@umbraco-cms/backoffice/external/lit';
import { routeOf, type Direction, type RouteEnd } from './direction.js';

/**
 * Which side a copy reads and which it writes, at a glance: the two ends with their pane icon and colour, this
 * environment left and the remote right (as the trees' pane heads), and an arrow from the source's colour into the
 * target's, pointing at the target. Shown above the dry run and in a node's diff when fields can be picked. Needs
 * brandStyles and routeStyles in the element's styles.
 */
export function renderRoute(direction: Direction, remoteName: string) {
  const { left, right, pointsLeft } = routeOf(direction, remoteName);
  const end = (side: RouteEnd) =>
    html`<div class="route-end ${side.side}">
      <umb-icon name=${side.icon}></umb-icon>
      <span><small>${side.role}</small><strong>${side.name}</strong></span>
    </div>`;
  return html`<div class="route">
    ${end(left)}
    <svg class="route-arrow ${pointsLeft ? 'points-left' : ''}" viewBox="0 0 72 16" aria-hidden="true">
      <!-- One shape: a shaft fading from the source's colour into the target's, and a head that closes on its end. The
           gradient is in user space: a horizontal line has no height, so a bounding-box gradient would be dropped. -->
      <defs>
        <linearGradient id="route-fade" gradientUnits="userSpaceOnUse" x1="2" y1="8" x2="68" y2="8">
          <stop class="route-from" offset="0"></stop>
          <stop class="route-to" offset="1"></stop>
        </linearGradient>
      </defs>
      <path class="route-shaft" d="M2 8H68" stroke="url(#route-fade)"></path>
      <path class="route-head" d="M61 2L68 8L61 14"></path>
    </svg>
    ${end(right)}
  </div>`;
}

export const routeStyles = css`
  .route {
    display: flex;
    align-items: center;
    gap: var(--uui-size-space-4);
    margin-bottom: var(--uui-size-space-5);
  }

  .route .route-end {
    --side: var(--transfr-color-local);
    flex: 1 1 0;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: var(--uui-size-space-3);
    padding: var(--uui-size-space-3) var(--uui-size-space-4);
    border: 1px solid var(--side);
    border-left-width: 6px;
    border-radius: var(--uui-border-radius);
    background: color-mix(in srgb, var(--side) 10%, var(--uui-color-surface));
  }

  .route .route-end.remote {
    --side: var(--transfr-color-remote);
  }

  .route .route-end umb-icon {
    flex: none;
    font-size: var(--uui-size-6);
    color: var(--side);
  }

  .route .route-end span {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .route .route-end small {
    color: var(--uui-color-text-alt);
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }

  .route .route-end strong {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* The arrow runs from the source's colour into the target's, and points at the target. */
  .route .route-arrow {
    --from: var(--transfr-color-local);
    --to: var(--transfr-color-remote);
    flex: none;
    width: 72px;
    height: 16px;
    overflow: visible;
  }

  /* Mirrored, the shaft starts on the right: the remote, the source when the arrow points left. */
  .route .route-arrow.points-left {
    --from: var(--transfr-color-remote);
    --to: var(--transfr-color-local);
    transform: scaleX(-1);
  }

  .route .route-from {
    stop-color: var(--from);
  }

  .route .route-to {
    stop-color: var(--to);
  }

  .route .route-shaft,
  .route .route-head {
    fill: none;
    stroke-width: 2.5;
    stroke-linecap: round;
    stroke-linejoin: round;
  }

  .route .route-head {
    stroke: var(--to);
  }
`;
