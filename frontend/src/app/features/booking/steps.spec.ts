import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { submit, texts, type } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiClient } from '../../core/http/api-client';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { PageBrand } from '../../core/layout/page-brand';
import {
  Answers,
  CATEGORIES,
  RECEIPT,
  SALON,
  SLOTS,
  STAFF,
  defaultAnswers,
  fakeApi,
} from './booking-fixtures';
import { requireReceipt, requireService, requireSlot } from './booking.guards';
import { BookingService } from './booking.service';
import { BookingShell } from './booking-shell';
import { DetailsStep } from './details-step';
import { DoneStep } from './done-step';
import { ImprintPage } from './imprint-page';
import { MonthCalendar } from './month-calendar';
import { PersonStep } from './person-step';
import { ServiceStep } from './service-step';
import { StepHeader } from './step-header';
import { TimeStep } from './time-step';

const SPACES = new RegExp('[\\s\\u00a0\\u202f]+', 'g');
const plain = (text: string) => text.replace(SPACES, ' ');

async function settle(fixture: ComponentFixture<unknown>) {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
  }
  fixture.detectChanges();
}

async function prepare(answers: Answers = defaultAnswers()) {
  localStorage.clear();
  const api = fakeApi(answers);
  TestBed.configureTestingModule({
    providers: [provideTestI18n(), provideRouter([]), { provide: ApiClient, useValue: api }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const booking = TestBed.inject(BookingService);
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  booking.setSlug('haarwerk');
  return { api, booking, navigate };
}

async function mount<T>(
  component: Type<T>,
  answers?: Answers,
  before?: (booking: BookingService) => void,
) {
  const context = await prepare(answers);
  before?.(context.booking);
  const fixture = TestBed.createComponent(component);
  await settle(fixture);
  return { fixture, ...context };
}

const click = async (fixture: ComponentFixture<unknown>, element: Element | null | undefined) => {
  (element as HTMLElement).click();
  await settle(fixture);
};

const q = <E extends Element>(fixture: ComponentFixture<unknown>, selector: string) =>
  fixture.nativeElement.querySelector(selector) as E;
const qa = <E extends Element>(fixture: ComponentFixture<unknown>, selector: string) =>
  Array.from<E>(fixture.nativeElement.querySelectorAll(selector));

const withService = (booking: BookingService) => booking.selectService(CATEGORIES[0].services[0]);
const withSlot = (booking: BookingService) => {
  withService(booking);
  booking.selectDate('2026-11-04');
  booking.selectSlot(SLOTS.slots[0]);
};

describe('ServiceStep', () => {
  it('lists the services by category with duration, price and description', async () => {
    const { fixture } = await mount(ServiceStep);

    expect(texts(fixture, 'h3')).toEqual(['Damen', 'Herren']);
    expect(texts(fixture, '.name')).toEqual(['Haarschnitt', 'Färben', 'Bart']);
    const meta = texts(fixture, '.meta').map((t) => plain(t));
    expect(meta[0]).toBe('1 Std. · 45,00 €');
    expect(meta[1]).toBe('2 Std. · booking.priceFrom');
    expect(meta[2]).toBe('30 Min. · 15,00 €');
    expect(texts(fixture, '.description')).toEqual(['Mit Waschen']);
    expect(q(fixture, 'app-step-header h2').textContent).toBe('booking.steps.service.title');
  });

  it('chooses the service and goes on to the person', async () => {
    const { fixture, booking, navigate } = await mount(ServiceStep);

    await click(fixture, qa(fixture, '.card')[1]);

    expect(booking.selectedService()?.id).toBe('s2');
    expect(navigate).toHaveBeenCalledWith(['/', 'haarwerk', 'person']);
  });

  it('marks the service chosen earlier', async () => {
    const { fixture } = await mount(ServiceStep, undefined, withService);

    expect(qa(fixture, '.card').map((c) => c.getAttribute('aria-current'))).toEqual([
      'true',
      'false',
      'false',
    ]);
  });

  it('says so when nothing can be booked online', async () => {
    const { fixture } = await mount(ServiceStep, { ...defaultAnswers(), '/services': [] });

    expect(texts(fixture, 'p')).toEqual(['booking.steps.service.empty']);
  });

  it('offers a retry when loading fails', async () => {
    const { fixture, api } = await mount(ServiceStep, {
      ...defaultAnswers(),
      '/services': new ApiError(500, 'unknown', 'x'),
    });

    expect(texts(fixture, '[role=alert]')).toEqual(['Unbekannter Fehler']);
    const calls = api.get.mock.calls.length;
    await click(fixture, q(fixture, 'button'));
    expect(api.get.mock.calls.length).toBeGreaterThan(calls);
  });
});

describe('PersonStep', () => {
  it('preselects "anyone" and lists the people with their initials', async () => {
    const { fixture } = await mount(PersonStep, undefined, withService);

    const radios = qa<HTMLInputElement>(fixture, 'input[type=radio]');
    expect(radios.map((r) => r.checked)).toEqual([true, false, false]);
    expect(texts(fixture, '.name')).toEqual([
      'booking.steps.person.anyone',
      'Anna Beispiel',
      'Ben',
    ]);
    expect(texts(fixture, 'app-staff-avatar')).toEqual(['AB', 'B']);
  });

  it('remembers the chosen person and continues to the time', async () => {
    const { fixture, booking, navigate } = await mount(PersonStep, undefined, withService);

    const radios = qa<HTMLInputElement>(fixture, 'input[type=radio]');
    radios[2].click();
    await settle(fixture);
    expect(booking.selectedStaff()?.id).toBe('p2');
    expect(radios.map((r) => r.checked)).toEqual([false, false, true]);

    await click(fixture, q(fixture, '.primary-action button'));
    expect(navigate).toHaveBeenCalledWith(['/', 'haarwerk', 'time']);
  });

  it('can go back to "anyone"', async () => {
    const { fixture, booking } = await mount(PersonStep, undefined, (b) => {
      withService(b);
      b.selectStaff(STAFF[0]);
    });

    qa<HTMLInputElement>(fixture, 'input[type=radio]')[0].click();
    await settle(fixture);

    expect(booking.selectedStaff()).toBeNull();
  });

  it('links back to the services', async () => {
    const { fixture } = await mount(PersonStep, undefined, withService);

    expect(q(fixture, 'a.back').getAttribute('href')).toBe('/haarwerk');
  });
});

describe('TimeStep', () => {
  beforeEach(() => vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-11-03T09:00:00Z') }));
  afterEach(() => vi.useRealTimers());

  it('opens on the salon’s current month with the free days enabled', async () => {
    const { fixture, booking, api } = await mount(TimeStep, undefined, withService);

    expect(booking.month()).toBe('2026-11');
    expect(api.get).toHaveBeenCalledWith(
      '/public/salons/haarwerk/availability/days',
      expect.objectContaining({
        params: { serviceId: 's1', staffId: undefined, month: '2026-11' },
      }),
    );
    const enabled = qa<HTMLButtonElement>(fixture, '.day:not(:disabled)').map((b) =>
      b.textContent?.trim(),
    );
    expect(enabled).toEqual(['4', '5', '20']);
    expect(qa(fixture, '.day')).toHaveLength(30);
    expect(q(fixture, '.head h3').textContent).toContain('November 2026');
  });

  it('shows the start times of a chosen day in parts of the day and selects one', async () => {
    const { fixture, booking, navigate } = await mount(TimeStep, undefined, withService);

    await click(fixture, qa<HTMLButtonElement>(fixture, '.day:not(:disabled)')[0]);

    expect(booking.selectedDate()).toBe('2026-11-04');
    expect(texts(fixture, 'h3.part')).toEqual([
      'booking.steps.time.morning',
      'booking.steps.time.afternoon',
      'booking.steps.time.evening',
    ]);
    expect(texts(fixture, '.chip')).toEqual(['09:00', '09:30', '12:00', '17:00']);
    expect(q<HTMLButtonElement>(fixture, '.primary-action button').disabled).toBe(true);

    await click(fixture, qa(fixture, '.chip')[1]);
    expect(booking.selectedSlot()?.time).toBe('09:30');
    expect(qa(fixture, '.chip').map((c) => c.getAttribute('aria-pressed'))).toEqual([
      'false',
      'true',
      'false',
      'false',
    ]);

    await click(fixture, q(fixture, '.primary-action button'));
    expect(navigate).toHaveBeenCalledWith(['/', 'haarwerk', 'details']);
  });

  it('a new day clears the chosen time', async () => {
    const { fixture, booking } = await mount(TimeStep, undefined, withService);
    await click(fixture, qa<HTMLButtonElement>(fixture, '.day:not(:disabled)')[0]);
    await click(fixture, qa(fixture, '.chip')[0]);

    await click(fixture, qa<HTMLButtonElement>(fixture, '.day:not(:disabled)')[1]);

    expect(booking.selectedSlot()).toBeNull();
    expect(q<HTMLButtonElement>(fixture, '.primary-action button').disabled).toBe(true);
  });

  it('pages to the next month but not before the current one', async () => {
    const { fixture, booking } = await mount(TimeStep, undefined, withService);
    const [previous, next] = qa<HTMLButtonElement>(fixture, '.nav');
    expect(previous.disabled).toBe(true);

    await click(fixture, next);

    expect(booking.month()).toBe('2026-12');
    expect(qa<HTMLButtonElement>(fixture, '.nav')[0].disabled).toBe(false);
  });

  it('tells the visitor to pick a day first, and when a month has no free time', async () => {
    const { fixture } = await mount(
      TimeStep,
      { ...defaultAnswers(), '/availability/days': { month: '2026-11', days: [] } },
      withService,
    );

    expect(texts(fixture, '.empty')).toEqual(['booking.steps.time.noDays']);
    expect(qa<HTMLButtonElement>(fixture, '.day:not(:disabled)')).toHaveLength(0);
  });

  it('says so when a day has no start times any more', async () => {
    const { fixture } = await mount(
      TimeStep,
      { ...defaultAnswers(), '/availability/slots': { date: '2026-11-04', slots: [] } },
      withService,
    );

    await click(fixture, qa<HTMLButtonElement>(fixture, '.day:not(:disabled)')[0]);

    expect(texts(fixture, '.empty')).toEqual(['booking.steps.time.noTimes']);
  });

  it('opens on the month of the day chosen earlier', async () => {
    const { booking } = await mount(TimeStep, undefined, (b) => {
      withService(b);
      b.selectDate('2026-12-24');
    });

    expect(booking.month()).toBe('2026-12');
  });
});

describe('MonthCalendar', () => {
  async function calendar(inputs: Record<string, unknown>) {
    await prepare();
    const fixture = TestBed.createComponent(MonthCalendar);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await settle(fixture);
    return fixture;
  }

  it('starts the week on Monday and names every day fully for screen readers', async () => {
    const fixture = await calendar({
      month: '2026-11',
      currentMonth: '2026-11',
      availableDays: new Set(['2026-11-04']),
    });

    expect(texts(fixture, '.weekday')).toHaveLength(7);
    expect(texts(fixture, '.weekday')[0]).toMatch(/^Mo/);
    const button = qa<HTMLButtonElement>(fixture, '.day')[3];
    expect(button.getAttribute('aria-label')).toContain('4. November 2026');
  });

  it('marks the selected and the current day and reports clicks', async () => {
    const fixture = await calendar({
      month: '2026-11',
      currentMonth: '2026-11',
      today: '2026-11-03',
      selected: '2026-11-04',
      availableDays: new Set(['2026-11-04', '2026-11-05']),
    });
    const picked: string[] = [];
    fixture.componentInstance.dayPick.subscribe((date) => picked.push(date));

    await click(fixture, qa(fixture, '.day')[4]);

    expect(picked).toEqual(['2026-11-05']);
    expect(qa(fixture, '.day')[3].getAttribute('aria-pressed')).toBe('true');
    expect(qa(fixture, '.day')[2].classList.contains('today')).toBe(true);
  });

  it('disables every day while loading', async () => {
    const fixture = await calendar({
      month: '2026-11',
      currentMonth: '2026-11',
      availableDays: new Set(['2026-11-04']),
      loading: true,
    });

    expect(qa<HTMLButtonElement>(fixture, '.day').every((b) => b.disabled)).toBe(true);
    expect(q(fixture, '.grid').getAttribute('aria-busy')).toBe('true');
  });

  it('limits paging to a year ahead', async () => {
    const fixture = await calendar({ month: '2027-11', currentMonth: '2026-11' });

    expect(qa<HTMLButtonElement>(fixture, '.nav').map((b) => b.disabled)).toEqual([false, true]);
  });
});

describe('DetailsStep', () => {
  const fill = (
    fixture: ComponentFixture<unknown>,
    name = 'Kim Kundin',
    email = 'kim@example.com',
  ) => {
    type(fixture, 'input[autocomplete=name]', name);
    type(fixture, 'input[type=email]', email);
  };

  it('summarizes the booking in the salon’s time', async () => {
    const { fixture } = await mount(DetailsStep, undefined, (b) => {
      withSlot(b);
      b.selectStaff(STAFF[0]);
      b.selectDate('2026-11-04');
      b.selectSlot(SLOTS.slots[0]);
    });

    const summary = texts(fixture, '.summary dd').map((t) => plain(t));
    expect(summary).toEqual([
      'Haarschnitt',
      'Anna Beispiel',
      'Mi., 4. November 2026, 09:00',
      '1 Std.',
      '45,00 €',
    ]);
  });

  it('shows "anyone" when no person was chosen and links to the privacy policy', async () => {
    const { fixture } = await mount(DetailsStep, undefined, withSlot);

    expect(texts(fixture, '.summary dd')[1]).toBe('booking.steps.person.anyone');
    expect(q(fixture, '.privacy a').getAttribute('href')).toBe(SALON.privacyPolicyUrl);
    expect(q(fixture, '.privacy a').getAttribute('rel')).toContain('noopener');
  });

  it('needs name and a valid e-mail and a plausible phone number before anything is sent', async () => {
    const { fixture, api } = await mount(DetailsStep, undefined, withSlot);

    type(fixture, 'input[type=email]', 'kein-mail');
    type(fixture, 'input[type=tel]', 'call me');
    await submit(fixture);

    expect(texts(fixture, 'mat-error')).toEqual([
      'Dieses Feld ist erforderlich.',
      'Bitte gib eine gültige E-Mail-Adresse ein.',
      'Ungültige Telefonnummer.',
    ]);
    expect(api.post).not.toHaveBeenCalled();
  });

  it('books and moves to the receipt', async () => {
    const { fixture, api, navigate } = await mount(DetailsStep, undefined, withSlot);

    fill(fixture);
    type(fixture, 'textarea', 'Bitte ohne Föhn');
    await submit(fixture);

    expect(api.post).toHaveBeenCalledOnce();
    expect(api.post.mock.calls[0][1]).toEqual(
      expect.objectContaining({
        name: 'Kim Kundin',
        note: 'Bitte ohne Föhn',
        language: 'de',
        staffId: null,
      }),
    );
    expect(navigate).toHaveBeenCalledWith(['/', 'haarwerk', 'done']);
  });

  it('has a honeypot field that is hidden from people and assistive technology', async () => {
    const { fixture, api } = await mount(DetailsStep, undefined, withSlot);
    const trap = q(fixture, '.trap');

    expect(trap.getAttribute('aria-hidden')).toBe('true');
    expect(trap.querySelector('input')?.getAttribute('tabindex')).toBe('-1');

    fill(fixture);
    type(fixture, '.trap input', 'https://spam.example');
    await submit(fixture);
    expect(api.post.mock.calls[0][1]).toEqual(
      expect.objectContaining({ website: 'https://spam.example' }),
    );
  });

  it('explains when the time was taken meanwhile and offers to pick another, with fresh times', async () => {
    const { fixture, booking, navigate } = await mount(
      DetailsStep,
      {
        ...defaultAnswers(),
        '/appointment-requests': new ApiError(409, 'booking.slot_unavailable', 'x'),
      },
      withSlot,
    );

    fill(fixture);
    await submit(fixture);

    expect(texts(fixture, '[role=alert] p')).toEqual(['booking.slotGone']);
    expect(navigate).not.toHaveBeenCalled();

    await click(fixture, q(fixture, '[role=alert] button'));
    expect(booking.selectedSlot()).toBeNull();
    expect(booking.selectedDate()).toBeNull();
    expect(booking.selectedService()).not.toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/', 'haarwerk', 'time']);
  });

  it('shows other errors in words and keeps the form filled', async () => {
    const { fixture } = await mount(
      DetailsStep,
      {
        ...defaultAnswers(),
        '/appointment-requests': new ApiError(422, 'booking.too_many_open_requests', 'email'),
      },
      withSlot,
    );

    fill(fixture);
    await submit(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Unbekannter Fehler']);
    expect((q(fixture, 'input[autocomplete=name]') as HTMLInputElement).value).toBe('Kim Kundin');
  });

  it('keeps what was typed when the visitor goes back and returns', async () => {
    const { booking } = await mount(DetailsStep, undefined, withSlot);
    booking.contact.update((c) => ({ ...c, name: 'Kim' }));

    TestBed.createComponent(DetailsStep).detectChanges();

    expect(booking.contact().name).toBe('Kim');
  });
});

describe('DoneStep', () => {
  it('says the request was sent while the salon has to confirm', async () => {
    const { fixture } = await mount(DoneStep, undefined, (b) => b.receipt.set(RECEIPT));

    expect(texts(fixture, 'h2')).toEqual(['booking.done.requestedTitle']);
    expect(texts(fixture, '.lead')).toEqual(['booking.done.requestedText']);
    const summary = texts(fixture, 'dd').map((t) => plain(t));
    expect(summary).toEqual(['Haarschnitt', 'Anna Beispiel', 'Mi., 4. November 2026, 09:00']);
    expect(document.activeElement).toBe(q(fixture, 'h2'));
  });

  it('says the appointment is confirmed with automatic confirmation', async () => {
    const { fixture } = await mount(DoneStep, undefined, (b) =>
      b.receipt.set({ ...RECEIPT, status: 'Confirmed', respondByUtc: null }),
    );

    expect(texts(fixture, 'h2')).toEqual(['booking.done.confirmedTitle']);
    expect(texts(fixture, '.lead')).toEqual(['booking.done.confirmedText']);
  });

  it('offers the calendar file and the Google and Outlook links once the appointment is confirmed', async () => {
    const { fixture } = await mount(DoneStep, undefined, (b) =>
      b.receipt.set({ ...RECEIPT, status: 'Confirmed', respondByUtc: null }),
    );

    const links = qa<HTMLAnchorElement>(fixture, 'app-calendar-actions a');
    expect(links.map((a) => plain(a.textContent ?? '').trim())).toEqual([
      'download booking.done.downloadIcs',
      'booking.done.googleCalendar',
      'booking.done.outlookCalendar',
    ]);
    expect(links[0].getAttribute('href')).toBe(
      '/api/public/appointments/secret-token/calendar.ics',
    );
    expect(links[0].hasAttribute('download')).toBe(true);
    expect(links[1].href).toContain('https://calendar.google.com/calendar/render?action=TEMPLATE');
    expect(links[2].href).toContain('https://outlook.live.com/calendar/0/deeplink/compose');
    for (const external of links.slice(1)) {
      expect(external.target).toBe('_blank');
      expect(external.rel).toBe('noopener noreferrer');
    }
  });

  it('does not offer the calendar while the request is still open', async () => {
    const { fixture } = await mount(DoneStep, undefined, (b) => b.receipt.set(RECEIPT));

    expect(qa(fixture, 'app-calendar-actions')).toHaveLength(0);
  });

  it('starts a new booking at the salon', async () => {
    const { fixture, booking, navigate } = await mount(DoneStep, undefined, (b) => {
      withSlot(b);
      b.receipt.set(RECEIPT);
    });

    await click(fixture, q(fixture, 'button'));

    expect(booking.receipt()).toBeNull();
    expect(booking.selectedService()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/', 'haarwerk']);
  });
});

describe('BookingShell', () => {
  async function shell(answers?: Answers, slug = 'haarwerk') {
    const context = await prepare(answers);
    const fixture = TestBed.createComponent(BookingShell);
    fixture.componentRef.setInput('slug', slug);
    await settle(fixture);
    return { fixture, ...context };
  }

  it('shows the salon with address, phone, opening hours, imprint and privacy', async () => {
    const { fixture } = await shell();

    expect(texts(fixture, 'h1')).toEqual(['Haarwerk Müller']);
    const links = qa<HTMLAnchorElement>(fixture, '.contact a');
    expect(links[0].href).toBe(
      'https://www.openstreetmap.org/search?query=Hauptstra%C3%9Fe%201%2C%2010115%20Berlin',
    );
    expect(links[0].target).toBe('_blank');
    expect(links[0].rel).toContain('noopener');
    expect(links[1].getAttribute('href')).toBe('tel:+4930123456');
    const hours = texts(fixture, '.hours dd').map((t) => t.replace(/\s+/g, ' '));
    expect(hours[0]).toBe('09:00–18:00');
    expect(hours[1]).toBe('09:00–12:00, 14:00–18:00');
    expect(hours[2]).toBe('booking.closed');
    expect(texts(fixture, '.hours dt')[0]).toBe('Montag');
    const footer = qa<HTMLAnchorElement>(fixture, 'footer a');
    expect(footer[0].getAttribute('href')).toBe('/haarwerk/imprint');
    expect(footer[1].href).toBe(SALON.privacyPolicyUrl);
  });

  it('keeps the hours collapsed until asked', async () => {
    const { fixture } = await shell();

    expect((q(fixture, 'details') as HTMLDetailsElement).open).toBe(false);
  });

  it('puts the salon’s name into the browser title and takes it out again when closed', async () => {
    const { fixture } = await shell();
    const names: (string | null)[] = [];
    TestBed.inject(PageBrand).name$.subscribe((name) => names.push(name));

    expect(names.at(-1)).toBe('Haarwerk Müller');
    fixture.destroy();
    expect(names.at(-1)).toBeNull();
  });

  it('works with an upper-case slug from the address bar', async () => {
    const { booking } = await shell(undefined, 'HaarWerk');

    expect(booking.slug()).toBe('haarwerk');
  });

  it('says so when the salon does not exist', async () => {
    const { fixture } = await shell({
      ...defaultAnswers(),
      '': new ApiError(404, 'salon.not_found', 'slug'),
    });

    expect(texts(fixture, '[role=alert] h1')).toEqual(['booking.notFound.title']);
    expect(fixture.nativeElement.querySelector('footer')).toBeNull();
  });

  it('shows a general error for other problems', async () => {
    const { fixture } = await shell({
      ...defaultAnswers(),
      '': new ApiError(500, 'server.unexpected', 'x'),
    });

    expect(texts(fixture, '[role=alert]')).toEqual(['Unbekannter Fehler']);
  });
});

describe('ImprintPage', () => {
  it('shows the imprint text as written and links back', async () => {
    const { fixture } = await mount(ImprintPage, undefined, () => undefined);
    await TestBed.inject(BookingService).salon.reload();
    await settle(fixture);

    expect(q(fixture, 'p').textContent).toBe('Haarwerk Müller\nHauptstraße 1');
    expect(q(fixture, 'a').getAttribute('href')).toBe('/haarwerk');
  });
});

describe('StepHeader', () => {
  it('shows the progress and takes focus', async () => {
    await prepare();
    const fixture = TestBed.createComponent(StepHeader);
    fixture.componentRef.setInput('step', 3);
    fixture.componentRef.setInput('titleKey', 'booking.steps.time.title');
    fixture.componentRef.setInput('backLink', '/haarwerk/person');
    await settle(fixture);

    expect(texts(fixture, '.count')).toEqual(['booking.stepOf']);
    expect(qa(fixture, '.bar span').map((s) => s.classList.contains('done'))).toEqual([
      true,
      true,
      true,
      false,
    ]);
    expect(q(fixture, 'a.back').getAttribute('href')).toBe('/haarwerk/person');
    expect(document.activeElement).toBe(q(fixture, 'h2'));
  });

  it('has no back link on the first step', async () => {
    await prepare();
    const fixture = TestBed.createComponent(StepHeader);
    fixture.componentRef.setInput('step', 1);
    fixture.componentRef.setInput('titleKey', 'booking.steps.service.title');
    await settle(fixture);

    expect(fixture.nativeElement.querySelector('a.back')).toBeNull();
  });
});

describe('step guards', () => {
  const route = (slug = 'haarwerk') =>
    ({ pathFromRoot: [{ params: {} }, { params: { slug } }] }) as never;
  const run = (guard: typeof requireService, slug?: string) =>
    TestBed.runInInjectionContext(() => guard(route(slug), {} as never));

  it('send visitors back to the start when the choice is missing', async () => {
    await prepare();

    const result = run(requireService);

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/haarwerk');
    expect(run(requireSlot)).toBeInstanceOf(UrlTree);
    expect(run(requireReceipt)).toBeInstanceOf(UrlTree);
  });

  it('let visitors pass once the earlier steps are done', async () => {
    const { booking } = await prepare();
    withService(booking);
    expect(run(requireService)).toBe(true);
    expect(run(requireSlot)).toBeInstanceOf(UrlTree);

    booking.selectSlot(SLOTS.slots[0]);
    expect(run(requireSlot)).toBe(true);

    booking.receipt.set(RECEIPT);
    expect(run(requireReceipt)).toBe(true);
  });

  it('start over for a different salon', async () => {
    const { booking } = await prepare();
    withService(booking);

    const result = run(requireService, 'Anderer-Salon');

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/anderer-salon');
    expect(booking.selectedService()).toBeNull();
  });
});
