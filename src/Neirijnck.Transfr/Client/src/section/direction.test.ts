import { describe, expect, it } from 'vitest';
import { routeOf, sidesOf } from './direction.js';

describe('sidesOf', () => {
  it('names the remote as the target when copying to it', () => {
    expect(sidesOf('ToRemote', 'Test site B')).toEqual({
      way: 'to Test site B',
      onTarget: 'on Test site B',
      onSource: 'here',
      target: 'Test site B',
      targetNow: 'On Test site B now',
      overwrite: 'Overwrite on Test site B: replace its version there with this one',
    });
  });

  it('names this environment as the target when copying from the remote', () => {
    expect(sidesOf('FromRemote', 'Test site B')).toEqual({
      way: 'from Test site B',
      onTarget: 'here',
      onSource: 'on Test site B',
      target: 'This environment',
      targetNow: 'Here now',
      overwrite: 'Overwrite here: replace its version here with the one on Test site B',
    });
  });
});

describe('routeOf', () => {
  const here = { side: 'local', name: 'This environment', icon: 'icon-home' };
  const remote = { side: 'remote', name: 'Test site B', icon: 'icon-cloud' };

  it('keeps this environment left and points right, at the remote, when copying to it', () => {
    expect(routeOf('ToRemote', 'Test site B')).toEqual({
      left: { ...here, role: 'From' },
      right: { ...remote, role: 'To' },
      pointsLeft: false,
    });
  });

  it('keeps this environment left and points left, at it, when copying from the remote', () => {
    expect(routeOf('FromRemote', 'Test site B')).toEqual({
      left: { ...here, role: 'To' },
      right: { ...remote, role: 'From' },
      pointsLeft: true,
    });
  });
});
