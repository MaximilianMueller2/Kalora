/** The required steps the API reports on, in the order of the assistant. The fifth screen ("done") has no data of its own. */
export type OnboardingStepKey = 'profile' | 'hours' | 'services' | 'team';

export const ONBOARDING_STEPS: readonly OnboardingStepKey[] = [
  'profile',
  'hours',
  'services',
  'team',
];

export interface OnboardingStep {
  key: OnboardingStepKey;
  done: boolean;
}

export interface OnboardingStatus {
  steps: OnboardingStep[];
  /** Everything a customer needs to book is in place. */
  complete: boolean;
  bookingUrl: string;
}
