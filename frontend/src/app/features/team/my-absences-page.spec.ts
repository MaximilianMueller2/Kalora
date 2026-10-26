import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { STAFF, fakeAuth } from '../../../testing/auth-fixtures';
import { texts } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { AuthService } from '../auth/auth.service';
import { Me } from '../auth/auth.models';
import { MyAbsencesPage } from './my-absences-page';
import { fakeTeam } from './team-fixtures';
import { TeamService } from './team.service';

async function mount(user: Me) {
  localStorage.clear();
  const team = fakeTeam();
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: TeamService, useValue: team },
      { provide: AuthService, useValue: fakeAuth(user) },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(MyAbsencesPage);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, team };
}

describe('MyAbsencesPage', () => {
  it('manages the absences of the signed-in staff member’s own calendar column', async () => {
    const { fixture, team } = await mount(STAFF);

    expect(team.listAbsences).toHaveBeenCalledWith('st1');
    expect(fixture.nativeElement.querySelector('app-absences-panel')).not.toBeNull();
  });

  it('explains that a calendar column is needed first', async () => {
    const { fixture, team } = await mount({ ...STAFF, staffId: null });

    expect(team.listAbsences).not.toHaveBeenCalled();
    expect(texts(fixture, 'p')).toEqual(['team.absences.noCalendar']);
  });
});
