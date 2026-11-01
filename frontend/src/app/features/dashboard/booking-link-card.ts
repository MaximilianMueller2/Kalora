import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Toast } from '../../core/ui/toast';
import { DashboardService } from './dashboard.service';

/** The salon's booking link to hand out: copy it, a QR code as PNG and as printable poster, and a ready-made text for Instagram. */
@Component({
  selector: 'app-booking-link-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  templateUrl: './booking-link-card.html',
  styleUrl: './booking-link-card.scss',
})
export class BookingLinkCard {
  private readonly toast = inject(Toast);
  private readonly transloco = inject(TranslocoService);
  private readonly dashboard = inject(DashboardService);

  readonly url = input.required<string>();
  readonly salonName = input.required<string>();

  protected readonly qrPng = this.dashboard.qrPngUrl;
  protected readonly posterPdf = this.dashboard.posterPdfUrl;
  protected readonly qrBroken = signal(false);

  /** The text for a post or a story, in the language the owner uses the app in. */
  protected readonly instagramText = computed(() =>
    this.transloco.translate('dashboard.share.text', { salon: this.salonName(), url: this.url() }),
  );

  /** Focusing the link selects it, so it can be copied by hand where the clipboard is not available. */
  protected selectAll(event: Event): void {
    (event.target as HTMLInputElement).select();
  }

  protected copyLink(): Promise<void> {
    return this.copy(this.url(), 'dashboard.share.linkCopied');
  }

  protected copyText(): Promise<void> {
    return this.copy(this.instagramText(), 'dashboard.share.textCopied');
  }

  private async copy(text: string, confirmation: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.toast.show(confirmation);
    } catch {
      // Clipboard access can be refused (no permission, plain http); the text stays selectable on the page.
      this.toast.show('dashboard.share.copyFailed');
    }
  }
}
