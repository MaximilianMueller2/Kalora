import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { WEEK } from './dashboard-fixtures';
import { DashboardWeek } from './dashboard.models';
import { WeekCard } from './week-card';

async function mount(week: DashboardWeek = WEEK) {
  localStorage.clear();
  TestBed.configureTestingModule({ providers: [provideTestI18n()] });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(WeekCard);
  fixture.componentRef.setInput('week', week);
  fixture.detectChanges();
  await fixture.whenStable();
  return fixture;
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();
const tile = (fixture: ComponentFixture<unknown>, index: number) =>
  root(fixture).querySelectorAll<HTMLElement>('.tile')[index];

describe('WeekCard', () => {
  it('names the week and shows the four figures', async () => {
    const fixture = await mount();

    expect(plain(root(fixture).querySelector('.range')?.textContent)).toBe(
      '02.11.2026 – 08.11.2026',
    );
    expect(plain(tile(fixture, 0).querySelector('dd')?.textContent)).toBe('12');
    expect(plain(tile(fixture, 2).querySelector('dd')?.textContent)).toBe('1');
  });

  it('shows the utilization as number and as bar with its detail', async () => {
    const fixture = await mount();

    expect(plain(tile(fixture, 1).querySelector('dd')?.textContent)).toContain('46 %');
    const bar = tile(fixture, 1).querySelector<HTMLElement>('[role="progressbar"]');
    expect(bar?.getAttribute('aria-valuenow')).toBe('46');
    expect(bar?.querySelector<HTMLElement>('span')?.style.width).toBe('46%');
    expect(plain(tile(fixture, 1).querySelector('.detail')?.textContent)).toBe(
      'dashboard.week.utilizationDetail',
    );
  });

  it('marks the revenue clearly as an estimate in the currency of the salon', async () => {
    const fixture = await mount();
    const revenue = tile(fixture, 3);

    expect(revenue.classList.contains('estimate')).toBe(true);
    expect(plain(revenue.querySelector('.chip')?.textContent)).toBe('dashboard.week.estimate');
    expect(plain(revenue.querySelector('dd')?.textContent)).toBe('540,00 €');
    expect(plain(revenue.querySelector('.detail')?.textContent)).toBe('dashboard.week.revenueHint');
    expect(revenue.querySelector('.from')).toBeNull();
  });

  it('says "from" when list prices are "from" prices', async () => {
    const fixture = await mount({ ...WEEK, includesFromPrices: true });

    expect(plain(tile(fixture, 3).querySelector('.from')?.textContent)).toBe('dashboard.week.from');
  });

  it('copes with a week without anything', async () => {
    const fixture = await mount({
      ...WEEK,
      appointments: 0,
      bookedMinutes: 0,
      availableMinutes: 0,
      utilizationPercent: 0,
      noShows: 0,
      estimatedRevenueCents: 0,
    });

    expect(plain(tile(fixture, 1).querySelector('dd')?.textContent)).toContain('0 %');
    expect(plain(tile(fixture, 3).querySelector('dd')?.textContent)).toBe('0,00 €');
  });
});
