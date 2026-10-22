import { Signal, computed, inject } from '@angular/core';
import { ErrorMessageService } from '../../core/http/error-message.service';
import { LanguageService } from '../../core/i18n/language.service';

/** Text for the error code a failed request left in `code`; re-translates when the language changes. */
export function injectErrorMessage(code: Signal<string | null>): Signal<string | null> {
  const messages = inject(ErrorMessageService);
  const language = inject(LanguageService);
  return computed(() => {
    language.current();
    const value = code();
    return value ? messages.messageFor(value) : null;
  });
}
