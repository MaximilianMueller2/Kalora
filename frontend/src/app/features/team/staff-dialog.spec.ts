import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { submit, texts, type } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { InviteDialog } from './invite-dialog';
import { StaffDialog } from './staff-dialog';
import { ANNA, fakeTeam } from './team-fixtures';
import { STAFF_COLORS } from './team.models';
import { TeamService } from './team.service';

async function mount<T>(component: Type<T>, data: unknown) {
  localStorage.clear();
  const team = fakeTeam();
  const dialogRef = { close: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: TeamService, useValue: team },
      { provide: MAT_DIALOG_DATA, useValue: data },
      { provide: MatDialogRef, useValue: dialogRef },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, team, dialogRef };
}

const CREATE = { canLinkSelf: false, canInviteManager: false };

const click = (fixture: ComponentFixture<unknown>, element: Element | null) => {
  (element as HTMLElement).click();
  fixture.detectChanges();
};

const swatches = (fixture: ComponentFixture<unknown>) =>
  Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('.swatch'));

function choose(fixture: ComponentFixture<unknown>, value: string): void {
  const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
  select.value = value;
  select.dispatchEvent(new Event('input'));
  select.dispatchEvent(new Event('change'));
  fixture.detectChanges();
}

describe('StaffDialog (new person)', () => {
  it('creates a person with the chosen color and closes with the result', async () => {
    const { fixture, team, dialogRef } = await mount(StaffDialog, CREATE);
    expect(swatches(fixture).map((b) => b.getAttribute('aria-label'))).toEqual([...STAFF_COLORS]);
    expect(swatches(fixture)[0].getAttribute('aria-pressed')).toBe('true');

    type(fixture, 'input[type=text]', '  Cem ');
    click(fixture, swatches(fixture)[3]);
    await submit(fixture);

    expect(swatches(fixture).map((b) => b.getAttribute('aria-pressed'))).toEqual([
      'false',
      'false',
      'false',
      'true',
      'false',
      'false',
      'false',
      'false',
      'false',
      'false',
    ]);
    expect(team.create).toHaveBeenCalledWith({
      displayName: 'Cem',
      color: STAFF_COLORS[3],
      isBookableOnline: true,
      autoConfirm: false,
      linkToCurrentUser: false,
    });
    expect(team.invite).not.toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalledWith({
      staff: expect.objectContaining({ id: 'new', displayName: 'Cem' }),
      inviteError: null,
    });
  });

  it('invites the new person by e-mail after creating them', async () => {
    const { fixture, team, dialogRef } = await mount(StaffDialog, CREATE);

    type(fixture, 'input[type=text]', 'Cem');
    type(fixture, 'input[type=email]', ' cem@example.com ');
    await submit(fixture);

    expect(team.invite).toHaveBeenCalledWith({
      staffId: 'new',
      email: 'cem@example.com',
      role: 'Staff',
    });
    expect(dialogRef.close).toHaveBeenCalledWith(expect.objectContaining({ inviteError: null }));
  });

  it('keeps the created person and hands the invitation error to the caller', async () => {
    const { fixture, team, dialogRef } = await mount(StaffDialog, CREATE);
    const error = new ApiError(400, 'validation.email_invalid', 'email');
    team.invite.mockRejectedValue(error);

    type(fixture, 'input[type=text]', 'Cem');
    type(fixture, 'input[type=email]', 'cem@example.com');
    await submit(fixture);

    expect(dialogRef.close).toHaveBeenCalledWith({
      staff: expect.objectContaining({ id: 'new' }),
      inviteError: error,
    });
  });

  it('lets only owners choose the role of the invited person', async () => {
    const asManager = await mount(StaffDialog, CREATE);
    expect(asManager.fixture.nativeElement.querySelector('select')).toBeNull();
    TestBed.resetTestingModule();

    const asOwner = await mount(StaffDialog, { ...CREATE, canInviteManager: true });
    type(asOwner.fixture, 'input[type=text]', 'Mia');
    type(asOwner.fixture, 'input[type=email]', 'mia@example.com');
    choose(asOwner.fixture, 'Manager');
    await submit(asOwner.fixture);

    expect(asOwner.team.invite).toHaveBeenCalledWith({
      staffId: 'new',
      email: 'mia@example.com',
      role: 'Manager',
    });
  });

  it('rejects an empty name and a malformed e-mail address', async () => {
    const { fixture, team } = await mount(StaffDialog, CREATE);

    type(fixture, 'input[type=email]', 'not-an-email');
    await submit(fixture);

    expect(texts(fixture, 'mat-error')).toEqual([
      'Dieses Feld ist erforderlich.',
      'Bitte gib eine gültige E-Mail-Adresse ein.',
    ]);
    expect(team.create).not.toHaveBeenCalled();
  });

  it('takes the own calendar column instead of inviting when "this is me" is checked', async () => {
    const { fixture, team } = await mount(StaffDialog, { ...CREATE, canLinkSelf: true });
    expect(fixture.nativeElement.querySelector('input[type=email]')).not.toBeNull();

    type(fixture, 'input[type=text]', 'Maria');
    const checkboxes = fixture.nativeElement.querySelectorAll('mat-checkbox input');
    click(fixture, checkboxes[checkboxes.length - 1]);
    expect(fixture.nativeElement.querySelector('input[type=email]')).toBeNull();
    await submit(fixture);

    expect(team.create).toHaveBeenCalledWith(
      expect.objectContaining({ displayName: 'Maria', linkToCurrentUser: true }),
    );
    expect(team.invite).not.toHaveBeenCalled();
  });

  it('does not offer "this is me" to someone who already has a column', async () => {
    const { fixture } = await mount(StaffDialog, CREATE);

    expect(fixture.nativeElement.querySelectorAll('mat-checkbox')).toHaveLength(2);
  });

  it('shows the translated server error and stays open', async () => {
    const { fixture, team, dialogRef } = await mount(StaffDialog, { ...CREATE, canLinkSelf: true });
    team.create.mockRejectedValue(new ApiError(409, 'staff.already_linked', 'x'));

    type(fixture, 'input[type=text]', 'Maria');
    await submit(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Schon verknüpft.']);
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});

describe('StaffDialog (edit)', () => {
  it('is prefilled, has no invitation section and saves the active flag', async () => {
    const { fixture, team, dialogRef } = await mount(StaffDialog, { ...CREATE, staff: ANNA });
    expect(
      (fixture.nativeElement.querySelector('input[type=text]') as HTMLInputElement).value,
    ).toBe('Anna Müller');
    expect(fixture.nativeElement.querySelector('input[type=email]')).toBeNull();
    expect(swatches(fixture)[0].getAttribute('aria-pressed')).toBe('true');

    const checkboxes = fixture.nativeElement.querySelectorAll('mat-checkbox input');
    click(fixture, checkboxes[checkboxes.length - 1]);
    await submit(fixture);

    expect(team.update).toHaveBeenCalledWith('st1', {
      displayName: 'Anna Müller',
      color: '#7C5CFC',
      isBookableOnline: true,
      autoConfirm: false,
      isActive: false,
    });
    expect(team.create).not.toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalledWith({
      staff: expect.objectContaining({ id: 'st1', isActive: false }),
      inviteError: null,
    });
  });
});

describe('InviteDialog', () => {
  const data = { staff: { ...ANNA, hasLogin: false, role: null }, canInviteManager: false };

  it('sends the invitation for the person and closes with true', async () => {
    const { fixture, team, dialogRef } = await mount(InviteDialog, data);

    type(fixture, 'input[type=email]', 'anna@example.com');
    await submit(fixture);

    expect(team.invite).toHaveBeenCalledWith({
      staffId: 'st1',
      email: 'anna@example.com',
      role: 'Staff',
    });
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('needs a valid address', async () => {
    const { fixture, team } = await mount(InviteDialog, data);

    await submit(fixture);
    type(fixture, 'input[type=email]', 'nope');
    await submit(fixture);

    expect(texts(fixture, 'mat-error')).toEqual(['Bitte gib eine gültige E-Mail-Adresse ein.']);
    expect(team.invite).not.toHaveBeenCalled();
  });

  it('shows the error of the server and stays open', async () => {
    const { fixture, team, dialogRef } = await mount(InviteDialog, data);
    team.invite.mockRejectedValue(new ApiError(409, 'invitation.email_taken', 'x'));

    type(fixture, 'input[type=email]', 'anna@example.com');
    await submit(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Mail vergeben.']);
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
