import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { OpenRequestsBadge } from './open-requests-badge';
import { RequestsPage } from './requests-page';
import { RequestsService } from './requests.service';
import {
  controllableOffer,
  expiresIn,
  fakeBadge,
  fakeRequests,
  request,
} from './requests-fixtures';

async function mount(
  options: { list?: ReturnType<typeof request>[]; dialogResult?: unknown } = {},
) {
  localStorage.clear();
  const requests = fakeRequests(options.list);
  const badge = fakeBadge();
  const offers: ReturnType<typeof controllableOffer>[] = [];
  const toast = {
    offerUndo: vi.fn().mockImplementation(() => {
      const offer = controllableOffer();
      offers.push(offer);
      return offer;
    }),
  };
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.dialogResult) })),
  };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: RequestsService, useValue: requests },
      { provide: OpenRequestsBadge, useValue: badge },
      { provide: AuthService, useValue: fakeAuth(OWNER) },
      { provide: Toast, useValue: toast },
      { provide: MatDialog, useValue: dialog },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(RequestsPage);
  await settle(fixture);
  return { fixture, requests, badge, toast, dialog, offers };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const q = (fixture: ComponentFixture<unknown>, selector: string) =>
  fixture.nativeElement.querySelector(selector) as HTMLElement | null;
const qa = (fixture: ComponentFixture<unknown>, selector: string) =>
  Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll(selector));
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();
const button = (fixture: ComponentFixture<unknown>, label: string) =>
  qa(fixture, '.card button').find((b) => plain(b.textContent) === label) as HTMLButtonElement;

describe('RequestsPage', () => {
  beforeEach(() => {
    // jsdom has no layout, so it does not know how to scroll.
    Element.prototype.scrollIntoView = vi.fn();
  });

  it('shows each open request as a card with time, person, customer and message', async () => {
    const { fixture } = await mount({
      list: [request(), request({ id: 'r2', serviceName: 'Färben', customerNote: null })],
    });

    const cards = qa(fixture, '.card');
    expect(cards).toHaveLength(2);
    expect(plain(cards[0].querySelector('h2')?.textContent)).toBe('Haarschnitt');
    const facts = qa(fixture, '.card:first-child dd').map((dd) => plain(dd.textContent));
    expect([facts[0], facts[1], facts[3]]).toEqual([
      'Mi., 4. November 2026, 09:00 – 10:00',
      'Anna',
      'Bitte ohne Föhn',
    ]);
    expect(facts[2]).toMatch(/^Kim Kundin/);
    expect(qa(fixture, '.card:first-child .contact').map((a) => a.getAttribute('href'))).toEqual([
      'tel:+49 170 1234567',
      'mailto:kim@example.com',
    ]);
    expect(qa(fixture, '.card:nth-child(2) dt').map((dt) => plain(dt.textContent))).not.toContain(
      'requests.note',
    );
  });

  it('says how long the salon can still answer and marks urgent ones', async () => {
    const { fixture } = await mount({
      list: [
        request({ expiresAtUtc: expiresIn(3 * 3_600_000 + 30_000) }),
        request({ id: 'r2', expiresAtUtc: expiresIn(45 * 60_000 + 30_000) }),
      ],
    });

    const chips = qa(fixture, '.chip').map((chip) => plain(chip.textContent));
    expect(chips[0]).toContain('requests.remaining');
    expect(qa(fixture, '.card').map((card) => card.classList.contains('urgent'))).toEqual([
      false,
      true,
    ]);
  });

  it('cannot accept a request whose time is over', async () => {
    const { fixture } = await mount({ list: [request({ expiresAtUtc: expiresIn(-60_000) })] });

    expect(plain(q(fixture, '.chip')?.textContent)).toContain('requests.overdue');
    expect(button(fixture, 'requests.confirm').disabled).toBe(true);
    expect(button(fixture, 'requests.declineAction').disabled).toBe(false);
  });

  it('explains an empty inbox', async () => {
    const { fixture } = await mount({ list: [] });

    expect(qa(fixture, '.card')).toHaveLength(0);
    expect(plain(q(fixture, '.empty h2')?.textContent)).toBe('requests.empty.title');
  });

  it('tells when the list could not be loaded', async () => {
    localStorage.clear();
    const requests = fakeRequests();
    requests.list.mockRejectedValue(new Error('offline'));
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        { provide: RequestsService, useValue: requests },
        { provide: OpenRequestsBadge, useValue: fakeBadge() },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
        { provide: Toast, useValue: { offerUndo: vi.fn() } },
        { provide: MatDialog, useValue: { open: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(RequestsPage);

    await settle(fixture);

    expect(plain(q(fixture, '[role="alert"]')?.textContent)).toBe('requests.loadFailed');
  });

  describe('accepting', () => {
    it('hides the card at once and sends the answer only when the undo message has run out', async () => {
      const { fixture, requests, toast, offers, badge } = await mount();

      button(fixture, 'requests.confirm').click();
      await settle(fixture);

      expect(qa(fixture, '.card')).toHaveLength(0);
      expect(toast.offerUndo).toHaveBeenCalledWith('requests.confirmed', 'requests.undo');
      expect(requests.confirm).not.toHaveBeenCalled();

      requests.list.mockResolvedValue([]);
      offers[0].expire();
      await settle(fixture);

      expect(requests.confirm).toHaveBeenCalledWith('r1');
      expect(requests.list).toHaveBeenCalledTimes(2);
      expect(badge.refresh).toHaveBeenCalled();
      expect(qa(fixture, '.card')).toHaveLength(0);
    });

    it('brings the card back and sends nothing when the person presses undo', async () => {
      const { fixture, requests, offers } = await mount();

      button(fixture, 'requests.confirm').click();
      await settle(fixture);
      offers[0].undo();
      await settle(fixture);

      expect(qa(fixture, '.card')).toHaveLength(1);
      expect(requests.confirm).not.toHaveBeenCalled();
    });

    it('shows the card again with the real state when the server refuses', async () => {
      const { fixture, requests, offers } = await mount();
      requests.confirm.mockRejectedValue(new Error('conflict'));

      button(fixture, 'requests.confirm').click();
      await settle(fixture);
      offers[0].expire();
      await settle(fixture);

      expect(requests.confirm).toHaveBeenCalledOnce();
      expect(qa(fixture, '.card')).toHaveLength(1);
    });

    it('makes the answer final at once when the page is left during the undo time', async () => {
      const { fixture, requests, offers } = await mount();

      button(fixture, 'requests.confirm').click();
      await settle(fixture);
      fixture.destroy();
      await new Promise((resolve) => setTimeout(resolve));

      expect(offers[0].dismiss).toHaveBeenCalled();
      expect(requests.confirm).toHaveBeenCalledWith('r1');
    });

    it('answers the next card independently', async () => {
      const { fixture, requests, offers } = await mount({
        list: [request(), request({ id: 'r2' })],
      });

      button(fixture, 'requests.confirm').click();
      await settle(fixture);
      expect(qa(fixture, '.card')).toHaveLength(1);
      button(fixture, 'requests.confirm').click();
      await settle(fixture);
      offers.forEach((offer) => offer.expire());
      await settle(fixture);

      expect(requests.confirm.mock.calls.map((call) => call[0]).sort()).toEqual(['r1', 'r2']);
    });
  });

  describe('declining', () => {
    it('asks for an optional reason first and sends it with the answer', async () => {
      const { fixture, requests, dialog, toast, offers } = await mount({
        dialogResult: 'Betriebsausflug',
      });

      button(fixture, 'requests.declineAction').click();
      await settle(fixture);

      expect(dialog.open).toHaveBeenCalledOnce();
      expect(toast.offerUndo).toHaveBeenCalledWith('requests.declined', 'requests.undo');
      expect(qa(fixture, '.card')).toHaveLength(0);
      offers[0].expire();
      await settle(fixture);
      expect(requests.decline).toHaveBeenCalledWith('r1', 'Betriebsausflug');
    });

    it('sends no reason when the field stays empty', async () => {
      const { fixture, requests, offers } = await mount({ dialogResult: '' });

      button(fixture, 'requests.declineAction').click();
      await settle(fixture);
      offers[0].expire();
      await settle(fixture);

      expect(requests.decline).toHaveBeenCalledWith('r1', null);
    });

    it('does nothing when the dialog is cancelled', async () => {
      const { fixture, requests, toast } = await mount({ dialogResult: undefined });

      button(fixture, 'requests.declineAction').click();
      await settle(fixture);

      expect(toast.offerUndo).not.toHaveBeenCalled();
      expect(requests.decline).not.toHaveBeenCalled();
      expect(qa(fixture, '.card')).toHaveLength(1);
    });
  });

  describe('link from the mail', () => {
    it('highlights the card of the request in the link', async () => {
      const { fixture } = await mount({ list: [request(), request({ id: 'r2' })] });

      fixture.componentRef.setInput('id', 'r2');
      await settle(fixture);

      expect(qa(fixture, '.card').map((card) => card.classList.contains('highlight'))).toEqual([
        false,
        true,
      ]);
      expect(q(fixture, '.notice')).toBeNull();
    });

    it('says so when that request is no longer open', async () => {
      const { fixture } = await mount();

      fixture.componentRef.setInput('id', 'gone');
      await settle(fixture);

      expect(plain(q(fixture, '.notice')?.textContent)).toBe('requests.alreadyHandled');
    });
  });
});
