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
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { InviteDialog } from './invite-dialog';
import { StaffDialog } from './staff-dialog';
import { ANNA, BEN, fakeTeam } from './team-fixtures';
import { TeamDetailPage } from './team-detail-page';
import { StaffMember } from './team.models';
import { TeamService } from './team.service';

async function mount(
  person: StaffMember = ANNA,
  options: { dialogResult?: unknown; role?: 'Owner' | 'Manager' } = {},
) {
  localStorage.clear();
  const team = fakeTeam();
  team.get.mockResolvedValue(person);
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.dialogResult) })),
  };
  const toast = { show: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: TeamService, useValue: team },
      { provide: AuthService, useValue: fakeAuth({ ...OWNER, role: options.role ?? 'Owner' }) },
      { provide: MatDialog, useValue: dialog },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(TeamDetailPage);
  fixture.componentRef.setInput('id', person.id);
  await settle(fixture);
  return { fixture, team, dialog, toast };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

const actionButtons = (fixture: ComponentFixture<unknown>) =>
  texts(fixture, '.actions button').map((t) => t.replace(/\s+/g, ' '));

describe('TeamDetailPage', () => {
  it('shows the person with their account state and three tabs', async () => {
    const { fixture, team } = await mount();

    expect(team.get).toHaveBeenCalledWith('st1');
    expect(texts(fixture, 'h1')).toEqual(['Anna Müller']);
    expect(texts(fixture, 'app-staff-avatar')).toEqual(['AM']);
    expect(texts(fixture, '.meta').map((t) => t.replace(/\s+/g, ' '))).toEqual(['team.role.Owner']);
    const tabs = Array.from<HTMLAnchorElement>(
      fixture.nativeElement.querySelectorAll('a[mat-tab-link]'),
    );
    expect(tabs.map((a) => a.textContent?.trim())).toEqual([
      'team.tabs.services',
      'team.tabs.hours',
      'team.tabs.absences',
    ]);
    expect(tabs.map((a) => a.getAttribute('href'))).toEqual(['/services', '/hours', '/absences']);
    expect(fixture.nativeElement.querySelector('.back').getAttribute('href')).toBe('/app/team');
  });

  it('offers an invitation only to people without a login, and "again" while one is pending', async () => {
    expect(actionButtons((await mount(ANNA)).fixture)).toEqual(['team.edit']);
    TestBed.resetTestingModule();
    expect(actionButtons((await mount(BEN)).fixture)).toEqual(['team.invite.action', 'team.edit']);
    TestBed.resetTestingModule();
    expect(actionButtons((await mount({ ...BEN, invitationPending: true })).fixture)).toEqual([
      'team.invite.again',
      'team.edit',
    ]);
  });

  it('marks inactive people', async () => {
    const { fixture } = await mount({ ...BEN, isActive: false });

    expect(texts(fixture, '.meta')[0].replace(/\s+/g, ' ')).toBe(
      'team.status.noLogin · team.status.inactive',
    );
  });

  it('edits the person and shows the saved values at once', async () => {
    const { fixture, dialog, toast } = await mount(ANNA, {
      dialogResult: { staff: { ...ANNA, displayName: 'Anna K.' }, inviteError: null },
    });

    (fixture.nativeElement.querySelectorAll('.actions button')[0] as HTMLButtonElement).click();
    await settle(fixture);

    const [component, config] = dialog.open.mock.calls[0];
    expect(component).toBe(StaffDialog);
    expect(config.data).toEqual({ staff: ANNA, canLinkSelf: false, canInviteManager: true });
    expect(texts(fixture, 'h1')).toEqual(['Anna K.']);
    expect(toast.show).toHaveBeenCalledWith('team.saved');
  });

  it('invites the person and reloads their state', async () => {
    const { fixture, dialog, team, toast } = await mount(BEN, {
      dialogResult: true,
      role: 'Manager',
    });

    (fixture.nativeElement.querySelectorAll('.actions button')[0] as HTMLButtonElement).click();
    await settle(fixture);

    const [component, config] = dialog.open.mock.calls[0];
    expect(component).toBe(InviteDialog);
    expect(config.data).toEqual({ staff: BEN, canInviteManager: false });
    await vi.waitFor(() => expect(team.get).toHaveBeenCalledTimes(2));
    expect(toast.show).toHaveBeenCalledWith('team.invite.sent');
  });

  it('does not touch anything when a dialog is cancelled', async () => {
    const { fixture, team, toast } = await mount(BEN, { dialogResult: undefined });

    for (const button of Array.from<HTMLButtonElement>(
      fixture.nativeElement.querySelectorAll('.actions button'),
    )) {
      button.click();
      await settle(fixture);
    }

    expect(team.get).toHaveBeenCalledOnce();
    expect(toast.show).not.toHaveBeenCalled();
  });

  it('says so when the person does not exist', async () => {
    localStorage.clear();
    const team = fakeTeam();
    team.get.mockRejectedValue(new ApiError(404, 'staff.not_found', 'x'));
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        provideRouter([]),
        { provide: TeamService, useValue: team },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    const fixture = TestBed.createComponent(TeamDetailPage);
    fixture.componentRef.setInput('id', 'gone');
    await settle(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['team.notFound']);
  });
});
