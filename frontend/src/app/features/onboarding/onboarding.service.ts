import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { OnboardingStatus } from './onboarding.models';

/** How far the salon is on its way to being bookable; the server derives it from the data, nothing is stored on the way. */
@Injectable({ providedIn: 'root' })
export class OnboardingService {
  private readonly api = inject(ApiClient);

  status(): Promise<OnboardingStatus> {
    return firstValueFrom(this.api.get<OnboardingStatus>('/onboarding'));
  }
}
