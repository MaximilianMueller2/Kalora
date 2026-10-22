/** Translation key and parameters for the first validation error of a field (texts under `validation.<kind>`). */
export interface FieldErrorText {
  key: string;
  params: Record<string, unknown>;
}

export function fieldErrorText(errors: readonly { kind: string }[]): FieldErrorText | null {
  const first = errors[0];
  return first ? { key: `validation.${first.kind}`, params: { ...first } } : null;
}
