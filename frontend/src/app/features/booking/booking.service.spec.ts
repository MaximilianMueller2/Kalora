import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiClient } from '../../core/http/api-client';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { CATEGORIES, DAYS, RECEIPT, SLOTS, STAFF, fakeApi } from './booking-fixtures';
import { BookingService } from './booking.service';

async function setup(api = fakeApi()) {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [provideTestI18n(), { provide: ApiClient, useValue: api }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  await TestBed.inject(LanguageService).use('en');
  const booking = TestBed.inject(BookingService);
  return { booking, api };
}

/** Lets effects run and the (synchronous) fake answers arrive. */
async function settle() {
  TestBed.tick();
  await Promise.resolve();
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}

const URL = '/public/salons/haarwerk';

describe('BookingService', () => {
  beforeEach(() => localStorage.clear());

  it('loads nothing until it knows which salon it is for', async () => {
    const { api } = await setup();

    await settle();

    expect(api.get).not.toHaveBeenCalled();
  });

  it('loads the salon and its services for the slug', async () => {
    const { booking, api } = await setup();

    booking.setSlug('haarwerk');
    await settle();

    expect(api.get).toHaveBeenCalledWith(URL, expect.objectContaining({ silent: true }));
    expect(api.get).toHaveBeenCalledWith(`${URL}/services`, expect.anything());
    expect(booking.salon.value()?.name).toBe('Haarwerk Müller');
    expect(booking.categories.value()).toEqual(CATEGORIES);
  });

  it('asks for the people only once a service is chosen', async () => {
    const { booking, api } = await setup();
    booking.setSlug('haarwerk');
    await settle();
    expect(api.get).not.toHaveBeenCalledWith(`${URL}/staff`, expect.anything());

    booking.selectService(CATEGORIES[0].services[0]);
    await settle();

    expect(api.get).toHaveBeenCalledWith(
      `${URL}/staff`,
      expect.objectContaining({ params: { serviceId: 's1' } }),
    );
    expect(booking.staff.value()).toEqual(STAFF);
  });

  it('asks for free days and times with service, person and date', async () => {
    const { booking, api } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.selectStaff(STAFF[1]);
    booking.month.set('2026-11');
    booking.selectDate('2026-11-04');
    await settle();

    expect(api.get).toHaveBeenCalledWith(
      `${URL}/availability/days`,
      expect.objectContaining({ params: { serviceId: 's1', staffId: 'p2', month: '2026-11' } }),
    );
    expect(api.get).toHaveBeenCalledWith(
      `${URL}/availability/slots`,
      expect.objectContaining({ params: { serviceId: 's1', staffId: 'p2', date: '2026-11-04' } }),
    );
    expect(booking.days.value()).toEqual(DAYS);
    expect(booking.slots.value()).toEqual(SLOTS);
  });

  it('leaves the person out of the question for "anyone"', async () => {
    const { booking, api } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.month.set('2026-11');
    await settle();

    const call = api.get.mock.calls.find(([path]) => path === `${URL}/availability/days`);
    expect(call?.[1]).toEqual(
      expect.objectContaining({
        params: { serviceId: 's1', staffId: undefined, month: '2026-11' },
      }),
    );
  });

  it('a new service clears person, day and time; the same service keeps them', async () => {
    const { booking } = await setup();
    booking.setSlug('haarwerk');
    const [cut, color] = CATEGORIES[0].services;
    booking.selectService(cut);
    booking.selectStaff(STAFF[0]);
    booking.selectDate('2026-11-04');
    booking.selectSlot(SLOTS.slots[0]);

    booking.selectService(cut);
    expect([booking.selectedStaff(), booking.selectedDate(), booking.selectedSlot()]).toEqual([
      STAFF[0],
      '2026-11-04',
      SLOTS.slots[0],
    ]);

    booking.selectService(color);
    expect([booking.selectedStaff(), booking.selectedDate(), booking.selectedSlot()]).toEqual([
      null,
      null,
      null,
    ]);
    expect(booking.selectedService()).toBe(color);
  });

  it('a new person or day clears the time that was chosen before', async () => {
    const { booking } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.selectDate('2026-11-04');
    booking.selectSlot(SLOTS.slots[0]);

    booking.selectDate('2026-11-05');
    expect(booking.selectedSlot()).toBeNull();

    booking.selectDate('2026-11-04');
    booking.selectSlot(SLOTS.slots[0]);
    booking.selectStaff(STAFF[0]);
    expect([booking.selectedDate(), booking.selectedSlot()]).toEqual([null, null]);
  });

  it('opens the calendar on the chosen day’s month or on the given fallback, but never overrides a month the visitor paged to', async () => {
    const { booking } = await setup();
    booking.setSlug('haarwerk');

    booking.showMonth('2026-11');
    expect(booking.month()).toBe('2026-11');

    booking.month.set('2027-01');
    booking.showMonth('2026-11');
    expect(booking.month()).toBe('2027-01');

    booking.clearTime();
    booking.selectDate('2026-12-24');
    booking.month.set('');
    booking.showMonth('2026-11');
    expect(booking.month()).toBe('2026-12');
  });

  it('another salon starts from scratch, the same one keeps everything', async () => {
    const { booking } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.contact.update((c) => ({ ...c, name: 'Kim' }));

    booking.setSlug('haarwerk');
    expect(booking.selectedService()).not.toBeNull();

    booking.setSlug('anderer-salon');
    expect(booking.selectedService()).toBeNull();
    expect(booking.contact().name).toBe('');
    expect(booking.slug()).toBe('anderer-salon');
  });

  it('sends the request with trimmed values, empty optional fields as null and the language in use', async () => {
    const { booking, api } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.selectStaff(STAFF[0]);
    booking.selectSlot(SLOTS.slots[0]);
    booking.contact.set({
      name: ' Kim Kundin ',
      email: ' kim@example.com ',
      phone: '  ',
      note: '',
      website: '',
    });

    const receipt = await booking.submit();

    expect(api.post).toHaveBeenCalledWith(
      `${URL}/appointment-requests`,
      {
        serviceId: 's1',
        startUtc: '2026-11-04T08:00:00Z',
        staffId: 'p1',
        name: 'Kim Kundin',
        email: 'kim@example.com',
        phone: null,
        note: null,
        language: 'en',
        website: '',
      },
      { silent: true },
    );
    expect(receipt).toEqual(RECEIPT);
    expect(booking.receipt()).toEqual(RECEIPT);
  });

  it('books "anyone" without a person ID and passes the honeypot on as it is', async () => {
    const { booking, api } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.selectSlot(SLOTS.slots[0]);
    booking.contact.set({
      name: 'Bot',
      email: 'bot@example.com',
      phone: '',
      note: '',
      website: 'https://spam',
    });

    await booking.submit();

    expect(api.post.mock.calls[0][1]).toEqual(
      expect.objectContaining({ staffId: null, website: 'https://spam' }),
    );
  });

  it('fails with the error code of the API and keeps what was chosen', async () => {
    const { booking } = await setup(
      fakeApi({ '/appointment-requests': new ApiError(409, 'booking.slot_unavailable', 'x') }),
    );
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.selectSlot(SLOTS.slots[0]);

    await expect(booking.submit()).rejects.toMatchObject({ code: 'booking.slot_unavailable' });

    expect(booking.receipt()).toBeNull();
    expect(booking.selectedSlot()).toEqual(SLOTS.slots[0]);
  });

  it('cannot submit before service and time are chosen', async () => {
    const { booking, api } = await setup();
    booking.setSlug('haarwerk');

    await expect(booking.submit()).rejects.toThrow();

    expect(api.post).not.toHaveBeenCalled();
  });

  it('"book another" keeps the contact details but forgets the booking', async () => {
    const { booking } = await setup();
    booking.setSlug('haarwerk');
    booking.selectService(CATEGORIES[0].services[0]);
    booking.selectSlot(SLOTS.slots[0]);
    booking.contact.update((c) => ({ ...c, name: 'Kim', email: 'kim@example.com' }));
    await booking.submit();

    booking.startOver();

    expect([booking.selectedService(), booking.selectedSlot(), booking.receipt()]).toEqual([
      null,
      null,
      null,
    ]);
    expect(booking.contact()).toEqual(
      expect.objectContaining({ name: 'Kim', email: 'kim@example.com' }),
    );
  });
});
