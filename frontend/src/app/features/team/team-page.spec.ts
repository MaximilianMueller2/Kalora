import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { texts } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { Me } from '../auth/auth.models';
import { StaffDialog } from './staff-dialog';
import { ANNA, BEN, fakeTeam } from './team-fixtures';
import { TeamPage } from './team-page';
import { TeamService } from './team.service';

async function mount(
  options: { user?: Me; team?: ReturnType<typeof fakeTeam>; dialogResult?: unknown } = {},
) {
  localStorage.clear();
  const team = options.team ?? fakeTeam();
  const auth = fakeAuth(options.user ?? OWNER);
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.dialogResult) })),
  };
  const toast = { show: vi.fn() };
  const errors = { show: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: TeamService, useValue: team },
      { provide: AuthService, useValue: auth },
      { provide: MatDialog, useValue: dialog },
      { provide: Toast, useValue: toast },
      { provide: ErrorNotifier, useValue: errors },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(TeamPage);
  await settle(fixture);
  return { fixture, team, auth, dialog, toast, errors };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

const clickButton = async (fixture: ComponentFixture<unknown>, selector: string) => {
  (fixture.nativeElement.querySelector(selector) as HTMLElement).click();
  await settle(fixture);
};

const normalized = (values: string[]) => values.map((v) => v.replace(/\s+/g, ' '));

describe('TeamPage', () => {
  it('lists the people with initials, account state and flags, linking to their pages', async () => {
    const team = fakeTeam();
    team.list.mockResolvedValue([
      ANNA,
      { ...BEN, invitationPending: true },
      { ...BEN, id: 'st3', displayName: 'Cem', isActive: false },
      { ...BEN, id: 'st4', displayName: 'Dora', isBookableOnline: false },
    ]);
    const { fixture } = await mount({ team });

    expect(texts(fixture, '.name')).toEqual(['Anna Müller', 'Ben', 'Cem', 'Dora']);
    expect(texts(fixture, 'app-staff-avatar')).toEqual(['AM', 'B', 'C', 'D']);
    expect(normalized(texts(fixture, '.meta'))).toEqual([
      'team.role.Owner',
      'team.status.invited',
      'team.status.noLogin · team.status.inactive',
      'team.status.noLogin · team.status.notOnline',
    ]);
    const links = Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll('a.person'));
    expect(links.map((a) => a.getAttribute('href'))).toEqual(['/st1', '/st2', '/st3', '/st4']);
    expect(fixture.nativeElement.querySelector('.person.inactive .name').textContent).toBe('Cem');
  });

  it('offers "just me" in an empty team and creates the own calendar column', async () => {
    const team = fakeTeam();
    team.list.mockResolvedValueOnce([]).mockResolvedValue([ANNA]);
    const { fixture, auth, toast } = await mount({ team });
    expect(texts(fixture, '.empty h2')).toEqual(['team.empty.title']);

    await clickButton(fixture, '.empty-actions button');

    expect(team.create).toHaveBeenCalledWith({
      displayName: 'Maria Müller',
      color: '#7C5CFC',
      isBookableOnline: true,
      autoConfirm: false,
      linkToCurrentUser: true,
    });
    expect(auth.reload).toHaveBeenCalledOnce();
    expect(toast.show).toHaveBeenCalledWith('team.saved');
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(texts(fixture, '.name')).toEqual(['Anna Müller']);
    });
  });

  it('does not offer "just me" to someone who already has a calendar column', async () => {
    const team = fakeTeam();
    team.list.mockResolvedValue([]);
    const { fixture } = await mount({ team, user: { ...OWNER, staffId: 'st1' } });

    expect(texts(fixture, '.empty-actions button')).toEqual(['team.empty.addPerson']);
  });

  it('shows the error when "just me" fails', async () => {
    const team = fakeTeam();
    team.list.mockResolvedValue([]);
    const error = new ApiError(409, 'staff.already_linked', 'x');
    team.create.mockRejectedValue(error);
    const { fixture, errors } = await mount({ team });

    await clickButton(fixture, '.empty-actions button');

    expect(errors.show).toHaveBeenCalledWith(error);
  });

  it('opens the dialog for a new person and reloads, also refreshing the user if it was them', async () => {
    const { fixture, team, auth, toast, dialog } = await mount({
      dialogResult: { staff: { ...ANNA, id: 'new' }, inviteError: null },
    });

    await clickButton(fixture, '.head button');

    const [component, config] = dialog.open.mock.calls[0];
    expect(component).toBe(StaffDialog);
    expect(config.data).toEqual({ canLinkSelf: true, canInviteManager: true });
    await vi.waitFor(() => expect(team.list).toHaveBeenCalledTimes(2));
    expect(auth.reload).toHaveBeenCalledOnce();
    expect(toast.show).toHaveBeenCalledWith('team.saved');
  });

  it('tells the owner when the invitation could not be sent although the person was created', async () => {
    const inviteError = new ApiError(400, 'validation.email_invalid', 'x');
    const { fixture, errors, auth } = await mount({
      dialogResult: { staff: { ...BEN, id: 'new' }, inviteError },
    });

    await clickButton(fixture, '.head button');

    expect(errors.show).toHaveBeenCalledWith(inviteError);
    expect(auth.reload).not.toHaveBeenCalled();
  });

  it('does nothing when the dialog is cancelled', async () => {
    const { fixture, team, toast } = await mount({ dialogResult: undefined });

    await clickButton(fixture, '.head button');

    expect(team.list).toHaveBeenCalledOnce();
    expect(toast.show).not.toHaveBeenCalled();
  });

  it('only lets owners invite managers', async () => {
    const { fixture, dialog } = await mount({
      user: { ...OWNER, role: 'Manager', staffId: 'st9' },
    });

    await clickButton(fixture, '.head button');

    expect(dialog.open.mock.calls[0][1].data).toEqual({
      canLinkSelf: false,
      canInviteManager: false,
    });
  });

  it('shows an error when the team cannot be loaded', async () => {
    const team = fakeTeam();
    team.list.mockRejectedValue(new ApiError(500, 'unknown', 'x'));
    const { fixture } = await mount({ team });

    expect(texts(fixture, '[role=alert]')).toEqual(['Unbekannter Fehler']);
  });
});
