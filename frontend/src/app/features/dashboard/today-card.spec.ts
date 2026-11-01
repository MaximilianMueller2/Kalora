import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { DAY, ZONE, at, person } from './dashboard-fixtures';
import { DashboardPerson } from './dashboard.models';
import { TodayCard } from './today-card';

const NOW = Date.parse(at(9, 30)); // 10:30 local, during the first appointment

async function mount(people: DashboardPerson[], now = NOW) {
  localStorage.clear();
  TestBed.configureTestingModule({ providers: [provideTestI18n()] });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(TodayCard);
  fixture.componentRef.setInput('people', people);
  fixture.componentRef.setInput('date', DAY);
  fixture.componentRef.setInput('zone', ZONE);
  fixture.componentRef.setInput('now', now);
  fixture.detectChanges();
  await fixture.whenStable();
  return fixture;
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const qa = (fixture: ComponentFixture<unknown>, selector: string) =>
  Array.from(root(fixture).querySelectorAll<HTMLElement>(selector));
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();

describe('TodayCard', () => {
  it('draws a row per person with working time, appointments and gaps in percent of the day', async () => {
    const fixture = await mount([person()]);

    // The window is 09:00-17:00 local (8 hours).
    expect(plain(qa(fixture, '.who')[0].textContent)).toBe('Anna');
    expect(qa(fixture, '.work')[0].style.width).toBe('100%');
    const gap = qa(fixture, '.gap')[0];
    expect([gap.style.left, gap.style.width]).toEqual(['37.5%', '25%']); // 12:00-14:00
    const slots = qa(fixture, '.slot');
    expect(slots.map((slot) => [slot.style.left, slot.style.width])).toEqual([
      ['12.5%', '12.5%'],
      ['62.5%', '12.5%'],
    ]);
    expect(slots.map((slot) => slot.classList.contains('request'))).toEqual([false, true]);
  });

  it('shows the now line at the local time', async () => {
    const fixture = await mount([person()]);

    expect(qa(fixture, '.now')[0].style.left).toBe('18.75%'); // 10:30 of 09:00-17:00
  });

  it('has no now line on another day', async () => {
    const fixture = await mount([person()], Date.parse('2026-11-05T09:30:00Z'));

    expect(qa(fixture, '.now')).toHaveLength(0);
  });

  it('names the hours under the timeline', async () => {
    const fixture = await mount([person()]);

    expect(qa(fixture, '.marks span').map((mark) => mark.textContent)).toEqual([
      '09:00',
      '11:00',
      '13:00',
      '15:00',
      '17:00',
    ]);
  });

  it('lists the next appointments with time, customer, service and person, running ones marked', async () => {
    const fixture = await mount([person()]);

    const items = qa(fixture, '.next li');
    expect(items.map((item) => plain(item.querySelector('.time')?.textContent))).toEqual([
      '10:00',
      '14:00',
    ]);
    expect(plain(items[0].querySelector('.text')?.textContent)).toBe(
      'Kim KundinHaarschnitt · Anna',
    );
    expect(items[0].classList.contains('running')).toBe(true);
    expect(items[1].classList.contains('running')).toBe(false);
    expect(plain(items[1].querySelector('.chip')?.textContent)).toBe('dashboard.today.request');
  });

  it('orders the next appointments of all people by time and leaves out what is over', async () => {
    const ben = person({
      staffId: 's2',
      displayName: 'Ben',
      appointments: [
        {
          id: 'b1',
          status: 'Confirmed',
          startUtc: at(8),
          endUtc: at(9),
          serviceName: 'Bart',
          customerName: 'Over',
        },
        {
          id: 'b2',
          status: 'Confirmed',
          startUtc: at(11),
          endUtc: at(12),
          serviceName: 'Bart',
          customerName: 'Tom',
        },
        {
          id: 'b3',
          status: 'Completed',
          startUtc: at(9),
          endUtc: at(10),
          serviceName: 'Bart',
          customerName: 'Done',
        },
      ],
    });

    const fixture = await mount([person(), ben]);

    expect(
      qa(fixture, '.next li').map((item) => plain(item.querySelector('strong')?.textContent)),
    ).toEqual(['Kim Kundin', 'Tom', 'Rita Request']);
  });

  it('says what a person who does not work today looks like', async () => {
    const fixture = await mount([
      person({ working: [], gaps: [], appointments: [] }),
      person({ staffId: 's2', displayName: 'Ben' }),
    ]);

    expect(plain(qa(fixture, '.off')[0].textContent)).toBe('dashboard.today.off');
    expect(qa(fixture, '.row')).toHaveLength(2);
  });

  it('says so when nobody is in the calendar and when nothing is left today', async () => {
    const nobody = await mount([]);
    expect(plain(qa(nobody, '.empty')[0].textContent)).toBe('dashboard.today.nobody');
    TestBed.resetTestingModule();

    const nothing = await mount([person({ appointments: [] })]);
    expect(plain(qa(nothing, '.empty')[0].textContent)).toBe('dashboard.today.none');
  });
});
