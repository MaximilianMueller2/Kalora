import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../../testing/auth-fixtures';
import { click, submit, texts, type } from '../../../../testing/forms';
import { provideTestI18n } from '../../../../testing/test-i18n';
import { ApiError } from '../../../core/http/api-error';
import { LanguageService } from '../../../core/i18n/language.service';
import { Toast } from '../../../core/ui/toast';
import { emptySchedule } from '../../../shared/week-schedule/week-schedule.model';
import { AuthService } from '../../auth/auth.service';
import { Me } from '../../auth/auth.models';
import { BookingRules, Closure, SalonProfile } from '../settings.models';
import { SettingsService } from '../settings.service';
import { ClosuresTab } from './closures-tab';
import { HoursTab } from './hours-tab';
import { RulesTab, isWholeNumberInRange } from './rules-tab';
import { SalonTab } from './salon-tab';

const PROFILE: SalonProfile = {
  name: 'Haarwerk',
  slug: 'haarwerk',
  industry: 'Hair',
  street: '',
  postalCode: '',
  city: '',
  phone: '',
  timeZone: 'Europe/Berlin',
  defaultLanguage: 'de',
  imprintText: null,
  privacyPolicyUrl: null,
};

const RULES: BookingRules = {
  slotIntervalMinutes: 15,
  minLeadTimeMinutes: 120,
  bookingHorizonDays: 60,
  cancellationDeadlineHours: 24,
  autoConfirm: false,
  requestExpiryHours: 24,
  retentionMonths: 24,
};

function fakeSettings() {
  return {
    getProfile: vi.fn().mockResolvedValue(PROFILE),
    updateProfile: vi.fn().mockImplementation((profile: SalonProfile) => Promise.resolve(profile)),
    suggestSlug: vi.fn().mockResolvedValue('haarwerk-2'),
    getBookingRules: vi.fn().mockResolvedValue(RULES),
    updateBookingRules: vi.fn().mockImplementation((rules: BookingRules) => Promise.resolve(rules)),
    getOpeningHours: vi.fn().mockResolvedValue(emptySchedule()),
    updateOpeningHours: vi
      .fn()
      .mockImplementation((schedule: unknown) => Promise.resolve(schedule)),
    listClosures: vi.fn().mockResolvedValue([]),
    createClosures: vi.fn().mockResolvedValue([]),
    deleteClosure: vi.fn().mockResolvedValue(undefined),
  };
}

async function mount<T>(
  component: Type<T>,
  options: { user?: Me; setup?: (settings: ReturnType<typeof fakeSettings>) => void } = {},
) {
  localStorage.clear();
  const settings = fakeSettings();
  options.setup?.(settings);
  const toast = { show: vi.fn() };
  const auth = fakeAuth(options.user ?? OWNER);
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: SettingsService, useValue: settings },
      { provide: AuthService, useValue: auth },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, settings, toast, auth };
}

const alert = (fixture: ComponentFixture<unknown>) => texts(fixture, '[role=alert]');
const errors = (fixture: ComponentFixture<unknown>) => texts(fixture, 'mat-error');

describe('SalonTab', () => {
  beforeEach(() => localStorage.clear());

  it('shows the loaded profile', async () => {
    const { fixture } = await mount(SalonTab);

    expect(
      (fixture.nativeElement.querySelector('input[autocomplete=organization]') as HTMLInputElement)
        .value,
    ).toBe('Haarwerk');
    expect(
      (fixture.nativeElement.querySelector('input[spellcheck=false]') as HTMLInputElement).value,
    ).toBe('haarwerk');
  });

  it('validates before saving', async () => {
    const { fixture, settings } = await mount(SalonTab);

    type(fixture, 'input[autocomplete=organization]', '');
    type(fixture, 'input[type=tel]', 'call me');
    type(fixture, 'input[type=url]', 'datenschutz');
    type(fixture, 'input[spellcheck=false]', 'Bad Slug!');
    await submit(fixture);

    expect(settings.updateProfile).not.toHaveBeenCalled();
    expect(errors(fixture)).toEqual([
      'Dieses Feld ist erforderlich.',
      'Ungültiger Link.',
      'Ungültige Telefonnummer.',
      'Ungültige Adresse.',
    ]);
  });

  it('saves trimmed values, turns blank optional fields into null and confirms', async () => {
    const { fixture, settings, toast, auth } = await mount(SalonTab);

    type(fixture, 'input[autocomplete=organization]', 'Haarwerk & Söhne');
    type(fixture, 'input[type=tel]', '+49 30 123456');
    type(fixture, 'textarea', '   ');
    await submit(fixture);

    expect(settings.updateProfile).toHaveBeenCalledWith({
      ...PROFILE,
      name: 'Haarwerk & Söhne',
      phone: '+49 30 123456',
      imprintText: null,
      privacyPolicyUrl: null,
    });
    expect(toast.show).toHaveBeenCalledWith('settings.saved');
    // The shell shows the salon name, so the profile is fetched again.
    expect(auth.reload).toHaveBeenCalledOnce();
  });

  it('shows a server error in the active language and keeps the form', async () => {
    const { fixture } = await mount(SalonTab, {
      setup: (settings) =>
        settings.updateProfile.mockRejectedValue(new ApiError(409, 'settings.slug_taken', 'slug')),
    });

    await submit(fixture);

    expect(alert(fixture)).toEqual(['Link schon vergeben.']);
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });

  it('lets the owner change and suggest the slug but not a manager', async () => {
    const owner = await mount(SalonTab);
    expect(
      (owner.fixture.nativeElement.querySelector('input[spellcheck=false]') as HTMLInputElement)
        .disabled,
    ).toBe(false);
    click(owner.fixture, 'button[type=button]');
    await owner.fixture.whenStable();
    owner.fixture.detectChanges();
    expect(owner.settings.suggestSlug).toHaveBeenCalledWith('Haarwerk');
    expect(
      (owner.fixture.nativeElement.querySelector('input[spellcheck=false]') as HTMLInputElement)
        .value,
    ).toBe('haarwerk-2');
    TestBed.resetTestingModule();

    const manager = await mount(SalonTab, { user: { ...OWNER, role: 'Manager' } });
    expect(
      (manager.fixture.nativeElement.querySelector('input[spellcheck=false]') as HTMLInputElement)
        .disabled,
    ).toBe(true);
    expect(manager.fixture.nativeElement.querySelector('button[type=button]')).toBeNull();
  });

  it("offers the salon's own time zone even if it is not in the list", async () => {
    const { fixture } = await mount(SalonTab, {
      setup: (settings) =>
        settings.getProfile.mockResolvedValue({ ...PROFILE, timeZone: 'Asia/Tokyo' }),
    });

    const options = Array.from<HTMLOptionElement>(
      fixture.nativeElement.querySelectorAll('select option'),
    ).map((o) => o.value);
    expect(options).toContain('Asia/Tokyo');
    expect(options).toContain('Europe/Berlin');
  });
});

describe('HoursTab', () => {
  it('loads the week, saves it and confirms', async () => {
    const { fixture, settings, toast } = await mount(HoursTab, {
      setup: (s) =>
        s.getOpeningHours.mockResolvedValue({
          ...emptySchedule(),
          Monday: [{ start: '09:00', end: '18:00' }],
        }),
    });
    expect(fixture.nativeElement.querySelectorAll('input[type=time]')).toHaveLength(2);

    click(fixture, 'button.actions, .actions button');
    await fixture.whenStable();

    expect(settings.updateOpeningHours).toHaveBeenCalledOnce();
    expect(settings.updateOpeningHours.mock.calls[0][0].Monday).toEqual([
      { start: '09:00', end: '18:00' },
    ]);
    expect(toast.show).toHaveBeenCalledWith('settings.saved');
  });

  it('does not allow saving while a day has a problem', async () => {
    const { fixture, settings } = await mount(HoursTab, {
      setup: (s) =>
        s.getOpeningHours.mockResolvedValue({
          ...emptySchedule(),
          Monday: [{ start: '18:00', end: '09:00' }],
        }),
    });

    const save = fixture.nativeElement.querySelector('.actions button') as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    save.click();
    expect(settings.updateOpeningHours).not.toHaveBeenCalled();
  });

  it('shows the reason when the server refuses the plan', async () => {
    const { fixture } = await mount(HoursTab, {
      setup: (s) =>
        s.updateOpeningHours.mockRejectedValue(new ApiError(400, 'closure.date_in_past')),
    });

    click(fixture, '.actions button');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(alert(fixture)).toEqual(['Datum liegt in der Vergangenheit.']);
  });
});

describe('RulesTab', () => {
  it('checks the whole-number ranges used by the API', () => {
    expect(isWholeNumberInRange('24', 1, 72)).toBe(true);
    expect(isWholeNumberInRange('0', 1, 72)).toBe(false);
    expect(isWholeNumberInRange('73', 1, 72)).toBe(false);
    expect(isWholeNumberInRange('', 0, 10)).toBe(false);
    expect(isWholeNumberInRange('2.5', 0, 10)).toBe(false);
    expect(isWholeNumberInRange('abc', 0, 10)).toBe(false);
  });

  it('shows the loaded rules and refuses values out of range', async () => {
    const { fixture, settings } = await mount(RulesTab);
    const numbers = () =>
      Array.from<HTMLInputElement>(fixture.nativeElement.querySelectorAll('input[type=number]'));

    expect(numbers().map((input) => input.value)).toEqual(['120', '60', '24', '24', '24']);
    type(fixture, 'input[type=number]:nth-of-type(1)', '');
    const horizon = numbers()[1];
    horizon.value = '0';
    horizon.dispatchEvent(new Event('input'));
    await submit(fixture);

    expect(settings.updateBookingRules).not.toHaveBeenCalled();
    expect(errors(fixture).length).toBeGreaterThan(0);
  });

  it('sends numbers, not text, and the chosen interval and auto-confirm flag', async () => {
    const { fixture, settings, toast } = await mount(RulesTab);
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = '30';
    // A real browser fires both events when a user picks an option.
    select.dispatchEvent(new Event('input'));
    select.dispatchEvent(new Event('change'));
    const horizon = fixture.nativeElement.querySelectorAll(
      'input[type=number]',
    )[1] as HTMLInputElement;
    horizon.value = '90';
    horizon.dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('mat-checkbox input').click();
    fixture.detectChanges();
    await submit(fixture);

    expect(settings.updateBookingRules).toHaveBeenCalledWith({
      ...RULES,
      slotIntervalMinutes: 30,
      bookingHorizonDays: 90,
      autoConfirm: true,
    });
    expect(toast.show).toHaveBeenCalledWith('settings.saved');
  });
});

describe('ClosuresTab', () => {
  const closure = (id: string, date: string, reason: string | null = null): Closure => ({
    id,
    date,
    reason,
  });

  it('lists upcoming closures with their reason', async () => {
    const { fixture } = await mount(ClosuresTab, {
      setup: (s) =>
        s.listClosures.mockResolvedValue([
          closure('1', '2026-12-24', 'Heiligabend'),
          closure('2', '2026-12-31'),
        ]),
    });

    expect(texts(fixture, 'ul.list li .date')).toHaveLength(2);
    expect(texts(fixture, 'ul.list li .reason')).toEqual(['Heiligabend']);
    expect(texts(fixture, 'ul.list li .date')[0]).toContain('24');
  });

  it('adds a single day or a range and reloads the list', async () => {
    const { fixture, settings } = await mount(ClosuresTab);

    await submit(fixture);
    expect(settings.createClosures).not.toHaveBeenCalled();
    expect(errors(fixture)).toEqual(['Dieses Feld ist erforderlich.']);

    type(fixture, 'input[type=date]', '2026-12-28');
    await submit(fixture);
    expect(settings.createClosures).toHaveBeenCalledWith({
      date: '2026-12-28',
      endDate: null,
      reason: null,
    });

    const inputs = fixture.nativeElement.querySelectorAll(
      'input[type=date]',
    ) as NodeListOf<HTMLInputElement>;
    for (const [index, value] of ['2026-12-28', '2026-12-30'].entries()) {
      inputs[index].value = value;
      inputs[index].dispatchEvent(new Event('input'));
    }
    type(fixture, 'input[type=text]', ' Betriebsurlaub ');
    await submit(fixture);
    expect(settings.createClosures).toHaveBeenLastCalledWith({
      date: '2026-12-28',
      endDate: '2026-12-30',
      reason: 'Betriebsurlaub',
    });
    expect(settings.listClosures.mock.calls.length).toBeGreaterThan(1);
  });

  it('rejects an end date before the start date', async () => {
    const { fixture, settings } = await mount(ClosuresTab);
    const inputs = fixture.nativeElement.querySelectorAll(
      'input[type=date]',
    ) as NodeListOf<HTMLInputElement>;
    for (const [index, value] of ['2026-12-30', '2026-12-28'].entries()) {
      inputs[index].value = value;
      inputs[index].dispatchEvent(new Event('input'));
    }
    await submit(fixture);

    expect(settings.createClosures).not.toHaveBeenCalled();
    expect(errors(fixture)).toEqual(['Ende vor Beginn.']);
  });

  it('shows what the server says about a bad date', async () => {
    const { fixture } = await mount(ClosuresTab, {
      setup: (s) =>
        s.createClosures.mockRejectedValue(new ApiError(400, 'closure.date_in_past', 'date')),
    });

    type(fixture, 'input[type=date]', '2020-01-01');
    await submit(fixture);

    expect(alert(fixture)).toEqual(['Datum liegt in der Vergangenheit.']);
  });

  it('removes a closure and confirms', async () => {
    const { fixture, settings, toast } = await mount(ClosuresTab, {
      setup: (s) => s.listClosures.mockResolvedValue([closure('abc', '2026-12-24')]),
    });

    click(fixture, 'ul.list li button');
    await fixture.whenStable();

    expect(settings.deleteClosure).toHaveBeenCalledWith('abc');
    expect(toast.show).toHaveBeenCalledWith('settings.removed');
  });
});
