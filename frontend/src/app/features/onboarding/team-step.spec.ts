import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { WeekSchedule, emptySchedule } from '../../shared/week-schedule/week-schedule.model';
import { AuthService } from '../auth/auth.service';
import { CatalogService } from '../services/catalog.service';
import { SettingsService } from '../settings/settings.service';
import { StaffMember } from '../team/team.models';
import { TeamService } from '../team/team.service';
import { ANNA, BEN, fakeTeam } from '../team/team-fixtures';
import { fakeCatalog, fakeSettings } from './onboarding-fixtures';
import { q, qa, settle, typeInto } from './step-helpers';
import { TeamStep } from './team-step';

const OPEN: WeekSchedule = { ...emptySchedule(), Tuesday: [{ start: '09:00', end: '18:00' }] };

async function mount(people: StaffMember[] = []) {
  localStorage.clear();
  const team = fakeTeam();
  team.list.mockResolvedValue(people);
  const settings = fakeSettings();
  settings.getOpeningHours.mockResolvedValue(OPEN);
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: TeamService, useValue: team },
      { provide: CatalogService, useValue: fakeCatalog() },
      { provide: SettingsService, useValue: settings },
      { provide: AuthService, useValue: fakeAuth(OWNER) },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(TeamStep);
  const saved = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  await settle(fixture);
  return { fixture, team, saved };
}

type Mounted = Awaited<ReturnType<typeof mount>>;
const next = async (m: Mounted) => {
  q<HTMLButtonElement>(m.fixture, 'button.next')!.click();
  await settle(m.fixture);
};

describe('TeamStep', () => {
  it('starts with the owner as the only person, doing all services', async () => {
    const { fixture } = await mount();

    expect(qa(fixture, 'fieldset.person')).toHaveLength(1);
    expect(q<HTMLInputElement>(fixture, 'fieldset.person input')!.value).toBe('Maria Müller');
    expect(qa<HTMLInputElement>(fixture, 'mat-checkbox input').map((box) => box.checked)).toEqual([
      true,
      true,
    ]);
  });

  it('creates the owner’s calendar column with all services and the opening hours as working hours', async () => {
    const m = await mount();

    await next(m);

    expect(m.team.create).toHaveBeenCalledWith({
      displayName: 'Maria Müller',
      color: '#7C5CFC',
      isBookableOnline: true,
      autoConfirm: false,
      linkToCurrentUser: true,
    });
    expect(m.team.saveSkills).toHaveBeenCalledWith('new', {
      skills: [
        { serviceId: 's1', durationOverrideMinutes: null },
        { serviceId: 's2', durationOverrideMinutes: null },
      ],
    });
    const [id, request] = m.team.saveSchedule.mock.calls[0];
    expect(id).toBe('new');
    expect(request.days).toHaveLength(7);
    expect(request.days.find((d: { weekday: string }) => d.weekday === 'Tuesday').blocks).toEqual(
      OPEN.Tuesday,
    );
    expect(request.validFrom).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(m.team.invite).not.toHaveBeenCalled();
    expect(m.saved).toHaveBeenCalledOnce();
  });

  it('takes away the services that were unticked', async () => {
    const m = await mount();

    qa<HTMLInputElement>(m.fixture, 'mat-checkbox input')[1].click();
    await settle(m.fixture);
    await next(m);

    expect(m.team.saveSkills).toHaveBeenCalledWith('new', {
      skills: [{ serviceId: 's1', durationOverrideMinutes: null }],
    });
  });

  it('adds more people, each with their own services, and invites those with an address', async () => {
    const m = await mount();

    q<HTMLButtonElement>(m.fixture, 'button.add')!.click();
    await settle(m.fixture);
    const [name, email] = qa<HTMLInputElement>(
      m.fixture,
      'fieldset.person:nth-of-type(2) input[type=text], fieldset.person:nth-of-type(2) input[type=email]',
    );
    typeInto(name, 'Tom');
    typeInto(email, 'tom@example.com');
    await next(m);

    expect(m.team.create).toHaveBeenCalledTimes(2);
    expect(m.team.create.mock.calls[1][0]).toMatchObject({
      displayName: 'Tom',
      linkToCurrentUser: false,
      color: '#2563EB',
    });
    expect(m.team.invite).toHaveBeenCalledWith({
      staffId: 'new',
      email: 'tom@example.com',
      role: 'Staff',
    });
  });

  it('removes a person that was added by mistake, but not the owner', async () => {
    const m = await mount();
    expect(q(m.fixture, 'button.remove')).toBeNull();

    q<HTMLButtonElement>(m.fixture, 'button.add')!.click();
    await settle(m.fixture);
    q<HTMLButtonElement>(m.fixture, 'button.remove')!.click();
    await settle(m.fixture);

    expect(qa(m.fixture, 'fieldset.person')).toHaveLength(1);
  });

  it('wants a name for everybody and a plausible address where one is given', async () => {
    const m = await mount();
    q<HTMLButtonElement>(m.fixture, 'button.add')!.click();
    await settle(m.fixture);

    await next(m);
    expect(q(m.fixture, '[role=alert]')).not.toBeNull();

    const inputs = qa<HTMLInputElement>(
      m.fixture,
      'fieldset.person:nth-of-type(2) input[type=text], fieldset.person:nth-of-type(2) input[type=email]',
    );
    typeInto(inputs[0], 'Tom');
    typeInto(inputs[1], 'kein-mail');
    await next(m);

    expect(q(m.fixture, '[role=alert]')).not.toBeNull();
    expect(m.team.create).not.toHaveBeenCalled();
    expect(m.saved).not.toHaveBeenCalled();
  });

  it('keeps the people that were created when a later one fails, so a retry does not create them twice', async () => {
    const m = await mount();
    q<HTMLButtonElement>(m.fixture, 'button.add')!.click();
    await settle(m.fixture);
    const inputs = qa<HTMLInputElement>(
      m.fixture,
      'fieldset.person:nth-of-type(2) input[type=text]',
    );
    typeInto(inputs[0], 'Tom');
    m.team.create
      .mockResolvedValueOnce({ ...BEN, id: 'one' })
      .mockRejectedValueOnce(new ApiError(409, 'subscription.staff_limit'));

    await next(m);

    expect(q(m.fixture, '[role=alert]')).not.toBeNull();
    expect(m.saved).not.toHaveBeenCalled();
    expect(qa(m.fixture, 'fieldset.person')).toHaveLength(1);

    m.team.create.mockResolvedValue({ ...BEN, id: 'two' });
    await next(m);

    expect(m.team.create).toHaveBeenCalledTimes(3);
    expect(m.saved).toHaveBeenCalledOnce();
  });

  it('goes on when an invitation cannot be sent, and says which person was not invited', async () => {
    const m = await mount();
    q<HTMLButtonElement>(m.fixture, 'button.add')!.click();
    await settle(m.fixture);
    const inputs = qa<HTMLInputElement>(
      m.fixture,
      'fieldset.person:nth-of-type(2) input[type=text], fieldset.person:nth-of-type(2) input[type=email]',
    );
    typeInto(inputs[0], 'Tom');
    typeInto(inputs[1], 'tom@example.com');
    m.team.invite.mockRejectedValue(new ApiError(500, 'server.unexpected'));

    await next(m);

    expect(m.saved).toHaveBeenCalledOnce();
    expect(q(m.fixture, '[role=status]')?.textContent).toContain('onboarding.team.inviteFailed');
  });

  it('lists people who exist already and creates nobody again', async () => {
    const m = await mount([ANNA, BEN]);

    expect(qa(m.fixture, '.existing li').map((li) => li.textContent?.trim())).toEqual([
      'Anna Müller',
      'Ben',
    ]);
    expect(qa(m.fixture, 'fieldset.person')).toHaveLength(0);

    await next(m);

    expect(m.team.create).not.toHaveBeenCalled();
    expect(m.saved).toHaveBeenCalledOnce();
  });

  it('does not list people who are no longer active', async () => {
    const { fixture } = await mount([{ ...BEN, isActive: false }]);

    expect(qa(fixture, '.existing li')).toHaveLength(0);
  });

  it('says so when the team cannot be loaded', async () => {
    localStorage.clear();
    const team = fakeTeam();
    team.list.mockRejectedValue(new Error('offline'));
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        provideRouter([]),
        { provide: TeamService, useValue: team },
        { provide: CatalogService, useValue: fakeCatalog() },
        { provide: SettingsService, useValue: fakeSettings() },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    const fixture = TestBed.createComponent(TeamStep);
    await settle(fixture);

    expect(q(fixture, '[role=alert]')?.textContent).toContain('Unbekannter Fehler');
  });
});
