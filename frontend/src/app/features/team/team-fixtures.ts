import { convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffMember } from './team.models';

export const ANNA: StaffMember = {
  id: 'st1',
  displayName: 'Anna Müller',
  color: '#7C5CFC',
  isBookableOnline: true,
  autoConfirm: false,
  isActive: true,
  sortOrder: 0,
  hasLogin: true,
  role: 'Owner',
  isMe: true,
  invitationPending: false,
};

export const BEN: StaffMember = {
  ...ANNA,
  id: 'st2',
  displayName: 'Ben',
  color: '#059669',
  hasLogin: false,
  role: null,
  isMe: false,
};

/** Stands in for TeamService in component specs; individual specs override what they need. */
export function fakeTeam() {
  return {
    list: vi.fn().mockResolvedValue([ANNA, BEN]),
    get: vi.fn().mockResolvedValue(ANNA),
    create: vi.fn().mockImplementation((request) =>
      Promise.resolve({
        ...BEN,
        id: 'new',
        displayName: request.displayName,
        color: request.color,
      }),
    ),
    update: vi.fn().mockImplementation((_id, request) => Promise.resolve({ ...ANNA, ...request })),
    invite: vi.fn().mockResolvedValue(undefined),
    getSkills: vi.fn().mockResolvedValue({ skills: [] }),
    saveSkills: vi.fn().mockImplementation((_id, skills) => Promise.resolve(skills)),
    getSchedule: vi.fn().mockResolvedValue({ versions: [] }),
    saveSchedule: vi.fn().mockResolvedValue({ versions: [] }),
    listAbsences: vi.fn().mockResolvedValue([]),
    createAbsence: vi.fn(),
    deleteAbsence: vi.fn().mockResolvedValue(undefined),
  };
}

/** A route whose parent carries `:id`, like the tabs below `team/:id`. */
export function routeWithStaffId(id = 'st1') {
  const paramMap = convertToParamMap({ id });
  return { parent: { paramMap: of(paramMap), snapshot: { paramMap } } };
}
