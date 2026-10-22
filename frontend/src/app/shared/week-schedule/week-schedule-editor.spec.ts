import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { WeekScheduleEditor } from './week-schedule-editor';
import { WeekSchedule, emptySchedule, setDayOpen } from './week-schedule.model';

async function mount(schedule: WeekSchedule = emptySchedule()) {
  localStorage.clear();
  TestBed.configureTestingModule({ providers: [provideTestI18n()] });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(WeekScheduleEditor);
  fixture.componentRef.setInput('schedule', schedule);
  fixture.detectChanges();
  return fixture;
}

const day = (fixture: ComponentFixture<WeekScheduleEditor>, index: number): HTMLElement =>
  fixture.nativeElement.querySelectorAll('fieldset.day')[index];
const times = (element: HTMLElement) =>
  Array.from(element.querySelectorAll<HTMLInputElement>('input[type=time]')).map((i) => i.value);
const buttonWith = (element: HTMLElement, text: string) =>
  Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.includes(text));

describe('WeekScheduleEditor', () => {
  beforeEach(() => localStorage.clear());

  it('shows the seven weekdays in the active language, starting with Monday', async () => {
    const fixture = await mount();

    const names = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('legend')).map(
      (l) => l.textContent?.trim(),
    );
    expect(names).toEqual([
      'Montag',
      'Dienstag',
      'Mittwoch',
      'Donnerstag',
      'Freitag',
      'Samstag',
      'Sonntag',
    ]);
  });

  it('opens a day with the usual hours when its box is ticked', async () => {
    const fixture = await mount();

    day(fixture, 0).querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.schedule().Monday).toEqual([{ start: '09:00', end: '18:00' }]);
    expect(times(day(fixture, 0))).toEqual(['09:00', '18:00']);
  });

  it('adds and removes time spans', async () => {
    const fixture = await mount(setDayOpen(emptySchedule(), 'Monday', true));

    buttonWith(day(fixture, 0), 'Zeitraum hinzufügen')!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.schedule().Monday).toHaveLength(2);

    (
      day(fixture, 0).querySelector('button[aria-label="Zeitraum entfernen"]') as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.schedule().Monday).toHaveLength(1);
  });

  it('takes over time changes into the schedule', async () => {
    const fixture = await mount(setDayOpen(emptySchedule(), 'Monday', true));
    const [start] = Array.from(
      day(fixture, 0).querySelectorAll<HTMLInputElement>('input[type=time]'),
    );

    start.value = '08:30';
    start.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(fixture.componentInstance.schedule().Monday[0]).toEqual({
      start: '08:30',
      end: '18:00',
    });
  });

  it('offers "same as the day before" from Tuesday on and copies it', async () => {
    const fixture = await mount(setDayOpen(emptySchedule(), 'Monday', true));

    expect(buttonWith(day(fixture, 0), 'Wie')).toBeUndefined();
    const copy = buttonWith(day(fixture, 1), 'Wie Montag');
    expect(copy).toBeDefined();
    copy!.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.schedule().Tuesday).toEqual([
      { start: '09:00', end: '18:00' },
    ]);
  });

  it('tells the user what is wrong with a day', async () => {
    const fixture = await mount({ ...emptySchedule(), Friday: [{ start: '18:00', end: '09:00' }] });

    const problem = day(fixture, 4).querySelector('.problem');
    expect(problem?.textContent?.trim()).toBe('Das Ende muss nach dem Beginn liegen.');
    expect(problem?.getAttribute('role')).toBe('alert');
    expect(day(fixture, 3).querySelector('.problem')).toBeNull();
  });

  it('labels every time field for screen readers with its weekday', async () => {
    const fixture = await mount(setDayOpen(emptySchedule(), 'Monday', true));

    const labels = Array.from(day(fixture, 0).querySelectorAll('.block label')).map((l) =>
      l.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(labels).toEqual(['Von, Montag', 'Bis, Montag']);
  });
});
