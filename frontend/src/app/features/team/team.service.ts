import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import {
  Absence,
  AbsenceCreated,
  CreateAbsenceRequest,
  CreateStaffRequest,
  InviteRequest,
  SaveScheduleRequest,
  SaveStaffRequest,
  StaffMember,
  StaffSchedule,
  StaffSkills,
} from './team.models';

/**
 * Team, skills, working hours and absences. Loading reports problems through the global snackbar; saving calls
 * are `silent` because the forms show the error code next to the input.
 */
@Injectable({ providedIn: 'root' })
export class TeamService {
  private readonly api = inject(ApiClient);

  list(): Promise<StaffMember[]> {
    return firstValueFrom(this.api.get<StaffMember[]>('/staff'));
  }

  get(id: string): Promise<StaffMember> {
    return firstValueFrom(this.api.get<StaffMember>(`/staff/${id}`));
  }

  create(request: CreateStaffRequest): Promise<StaffMember> {
    return firstValueFrom(this.api.post<StaffMember>('/staff', request, { silent: true }));
  }

  update(id: string, request: SaveStaffRequest): Promise<StaffMember> {
    return firstValueFrom(this.api.put<StaffMember>(`/staff/${id}`, request, { silent: true }));
  }

  invite(request: InviteRequest): Promise<void> {
    return firstValueFrom(this.api.post<void>('/invitations', request, { silent: true }));
  }

  getSkills(id: string): Promise<StaffSkills> {
    return firstValueFrom(this.api.get<StaffSkills>(`/staff/${id}/services`));
  }

  saveSkills(id: string, skills: StaffSkills): Promise<StaffSkills> {
    return firstValueFrom(
      this.api.put<StaffSkills>(`/staff/${id}/services`, skills, { silent: true }),
    );
  }

  getSchedule(id: string): Promise<StaffSchedule> {
    return firstValueFrom(this.api.get<StaffSchedule>(`/staff/${id}/working-hours`));
  }

  saveSchedule(id: string, request: SaveScheduleRequest): Promise<StaffSchedule> {
    return firstValueFrom(
      this.api.put<StaffSchedule>(`/staff/${id}/working-hours`, request, { silent: true }),
    );
  }

  listAbsences(id: string): Promise<Absence[]> {
    return firstValueFrom(this.api.get<Absence[]>(`/staff/${id}/absences`));
  }

  createAbsence(id: string, request: CreateAbsenceRequest): Promise<AbsenceCreated> {
    return firstValueFrom(
      this.api.post<AbsenceCreated>(`/staff/${id}/absences`, request, { silent: true }),
    );
  }

  deleteAbsence(id: string, absenceId: string): Promise<void> {
    return firstValueFrom(this.api.delete<void>(`/staff/${id}/absences/${absenceId}`));
  }
}
