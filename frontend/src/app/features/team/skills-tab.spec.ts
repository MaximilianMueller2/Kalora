import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { texts } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { Catalog } from '../services/catalog.models';
import { CatalogService } from '../services/catalog.service';
import { SkillsTab } from './skills-tab';
import { fakeTeam, routeWithStaffId } from './team-fixtures';
import { TeamService } from './team.service';

const service = (id: string, name: string, minutes: number, isActive = true) => ({
  id,
  categoryId: 'c1',
  name,
  description: null,
  priceCents: 1000,
  priceIsFrom: false,
  currency: 'EUR',
  durationMinutes: minutes,
  bufferBeforeMinutes: 0,
  bufferAfterMinutes: 0,
  isBookableOnline: true,
  isActive,
  sortOrder: 0,
  hasAppointments: false,
});

const CATALOG: Catalog = {
  categories: [
    {
      id: 'c1',
      name: 'Damen',
      sortOrder: 0,
      services: [service('s1', 'Schneiden', 30), service('s2', 'Färben', 90, false)],
    },
    { id: 'c2', name: 'Leer', sortOrder: 1, services: [] },
  ],
};

async function mount(
  options: {
    catalog?: Catalog;
    skills?: { serviceId: string; durationOverrideMinutes: number | null }[];
  } = {},
) {
  localStorage.clear();
  const team = fakeTeam();
  team.getSkills.mockResolvedValue({ skills: options.skills ?? [] });
  const catalog = { get: vi.fn().mockResolvedValue(options.catalog ?? CATALOG) };
  const toast = { show: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: ActivatedRoute, useValue: routeWithStaffId('st1') },
      { provide: TeamService, useValue: team },
      { provide: CatalogService, useValue: catalog },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(SkillsTab);
  await settle(fixture);
  return { fixture, team, toast };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

const checkboxes = (fixture: ComponentFixture<unknown>) =>
  Array.from<HTMLInputElement>(fixture.nativeElement.querySelectorAll('mat-checkbox input'));
const durationSelects = (fixture: ComponentFixture<unknown>) =>
  Array.from<HTMLSelectElement>(fixture.nativeElement.querySelectorAll('.duration select'));

async function toggle(fixture: ComponentFixture<unknown>, index: number) {
  checkboxes(fixture)[index].click();
  await settle(fixture);
}

async function setDuration(fixture: ComponentFixture<unknown>, index: number, value: string) {
  const select = durationSelects(fixture)[index];
  select.value = value;
  select.dispatchEvent(new Event('change'));
  await settle(fixture);
}

const save = async (fixture: ComponentFixture<unknown>) => {
  (fixture.nativeElement.querySelector('.actions button') as HTMLButtonElement).click();
  await settle(fixture);
};

describe('SkillsTab', () => {
  it('lists the services by category with the person’s skills ticked and their own durations', async () => {
    const { fixture } = await mount({
      skills: [
        { serviceId: 's1', durationOverrideMinutes: null },
        { serviceId: 's2', durationOverrideMinutes: 120 },
      ],
    });

    expect(texts(fixture, 'legend')).toEqual(['Damen']);
    expect(checkboxes(fixture).map((c) => c.checked)).toEqual([true, true]);
    expect(texts(fixture, 'mat-checkbox')[1]).toContain('services.service.inactiveBadge');
    expect(durationSelects(fixture).map((s) => s.value)).toEqual(['', '120']);
    expect(durationSelects(fixture)[0].options[0].textContent).toContain(
      'team.skills.sameAsService',
    );
  });

  it('hides the duration of services that are not ticked', async () => {
    const { fixture } = await mount({
      skills: [{ serviceId: 's1', durationOverrideMinutes: null }],
    });

    expect(durationSelects(fixture)).toHaveLength(1);
  });

  it('saves the ticked services; "same as the service" means no override', async () => {
    const { fixture, team, toast } = await mount({
      skills: [
        { serviceId: 's1', durationOverrideMinutes: null },
        { serviceId: 's2', durationOverrideMinutes: 120 },
      ],
    });

    await toggle(fixture, 0);
    await setDuration(fixture, 0, '45');
    await save(fixture);

    expect(team.saveSkills).toHaveBeenCalledWith('st1', {
      skills: [{ serviceId: 's2', durationOverrideMinutes: 45 }],
    });
    expect(toast.show).toHaveBeenCalledWith('team.saved');
  });

  it('adds a skill without a duration override and can fall back to the default again', async () => {
    const { fixture, team } = await mount({
      skills: [{ serviceId: 's2', durationOverrideMinutes: 60 }],
    });

    await toggle(fixture, 0);
    await setDuration(fixture, 1, '');
    await save(fixture);

    expect(team.saveSkills).toHaveBeenCalledWith('st1', {
      skills: [
        { serviceId: 's2', durationOverrideMinutes: null },
        { serviceId: 's1', durationOverrideMinutes: null },
      ],
    });
  });

  it('selects everything or nothing in one click and keeps own durations when selecting all', async () => {
    const { fixture, team } = await mount({
      skills: [{ serviceId: 's2', durationOverrideMinutes: 60 }],
    });
    const [all, none] = Array.from<HTMLButtonElement>(
      fixture.nativeElement.querySelectorAll('.bulk button'),
    );

    all.click();
    await settle(fixture);
    expect(checkboxes(fixture).map((c) => c.checked)).toEqual([true, true]);
    expect(durationSelects(fixture)[1].value).toBe('60');

    none.click();
    await settle(fixture);
    expect(checkboxes(fixture).map((c) => c.checked)).toEqual([false, false]);
    await save(fixture);
    expect(team.saveSkills).toHaveBeenCalledWith('st1', { skills: [] });
  });

  it('points to the services page when there are none yet', async () => {
    const { fixture } = await mount({ catalog: { categories: [] } });

    expect(texts(fixture, '.hint')[0]).toContain('team.skills.noServices');
    expect(fixture.nativeElement.querySelector('.hint a').getAttribute('href')).toBe(
      '/app/services',
    );
    expect(fixture.nativeElement.querySelector('.actions button')).toBeNull();
  });

  it('shows the error code of a failed save and keeps the selection', async () => {
    const { fixture, team } = await mount({
      skills: [{ serviceId: 's1', durationOverrideMinutes: null }],
    });
    team.saveSkills.mockRejectedValue(new ApiError(400, 'staff.schedule_empty', 'x'));

    await save(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Plan leer.']);
    expect(checkboxes(fixture).map((c) => c.checked)).toEqual([true, false]);
  });
});
