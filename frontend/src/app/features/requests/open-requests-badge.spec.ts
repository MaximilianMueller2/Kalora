import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { BADGE_REFRESH_MS, OpenRequestsBadge } from './open-requests-badge';
import { RequestsService } from './requests.service';

@Component({ selector: 'app-host', template: '' })
class Host {
  constructor() {
    TestBed.inject(OpenRequestsBadge).watch();
  }
}

function setup(counts: (number | Error)[]) {
  const requests = {
    count: vi.fn().mockImplementation(() => {
      const next = counts.length > 1 ? counts.shift()! : counts[0];
      return next instanceof Error ? Promise.reject(next) : Promise.resolve(next);
    }),
  };
  TestBed.configureTestingModule({ providers: [{ provide: RequestsService, useValue: requests }] });
  return { requests, badge: TestBed.inject(OpenRequestsBadge) };
}

describe('OpenRequestsBadge', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('asks at once, then every minute', async () => {
    const { requests, badge } = setup([2, 3, 0]);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    await vi.advanceTimersByTimeAsync(0);
    expect(badge.count()).toBe(2);

    await vi.advanceTimersByTimeAsync(BADGE_REFRESH_MS);
    expect(badge.count()).toBe(3);

    await vi.advanceTimersByTimeAsync(BADGE_REFRESH_MS);
    expect(badge.count()).toBe(0);
    expect(requests.count).toHaveBeenCalledTimes(3);
  });

  it('keeps the last number and carries on when a request fails', async () => {
    const { badge } = setup([4, new Error('offline'), 5]);
    TestBed.createComponent(Host).detectChanges();

    await vi.advanceTimersByTimeAsync(0);
    await vi.advanceTimersByTimeAsync(BADGE_REFRESH_MS);
    expect(badge.count()).toBe(4);

    await vi.advanceTimersByTimeAsync(BADGE_REFRESH_MS);
    expect(badge.count()).toBe(5);
  });

  it('stops asking when the component that started it is gone', async () => {
    const { requests } = setup([1]);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(0);

    fixture.destroy();
    await vi.advanceTimersByTimeAsync(BADGE_REFRESH_MS * 3);

    expect(requests.count).toHaveBeenCalledTimes(1);
  });

  it('refreshes right away when asked', async () => {
    const { badge } = setup([7]);

    await badge.refresh();

    expect(badge.count()).toBe(7);
  });

  it('ignores a failing refresh', async () => {
    const { badge } = setup([new Error('offline')]);

    await expect(badge.refresh()).resolves.toBeUndefined();
    expect(badge.count()).toBe(0);
  });
});
