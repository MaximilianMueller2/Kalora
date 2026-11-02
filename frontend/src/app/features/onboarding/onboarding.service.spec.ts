import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../core/http/api-client';
import { status } from './onboarding-fixtures';
import { OnboardingService } from './onboarding.service';

describe('OnboardingService', () => {
  it('reads the progress from the server, which derives it from the data', async () => {
    const api = { get: vi.fn().mockReturnValue(of(status('profile'))) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    const result = await TestBed.inject(OnboardingService).status();

    expect(api.get).toHaveBeenCalledWith('/onboarding');
    expect(result.steps.filter((step) => step.done).map((step) => step.key)).toEqual(['profile']);
  });
});
