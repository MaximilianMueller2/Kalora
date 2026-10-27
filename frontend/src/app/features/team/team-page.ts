import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  resource,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { StaffAvatar } from '../../shared/ui/staff-avatar';
import { StaffDialog, StaffDialogData, StaffDialogResult } from './staff-dialog';
import { StaffMember } from './team.models';
import { TeamService } from './team.service';

@Component({
  selector: 'app-team-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink, StaffAvatar, TranslocoPipe],
  styleUrl: './team-page.scss',
  template: `
    <header class="head">
      <h1>{{ 'team.title' | transloco }}</h1>
      @if (team.hasValue() && team.value().length > 0) {
        <button mat-flat-button type="button" (click)="add()">
          {{ 'team.add' | transloco }}
        </button>
      }
    </header>

    @if (team.hasValue()) {
      @if (team.value().length === 0) {
        <section class="empty" aria-labelledby="empty-title">
          <mat-icon aria-hidden="true">group</mat-icon>
          <h2 id="empty-title">{{ 'team.empty.title' | transloco }}</h2>
          <p>{{ 'team.empty.text' | transloco }}</p>
          <div class="empty-actions">
            @if (canLinkSelf()) {
              <button mat-flat-button type="button" [disabled]="busy()" (click)="justMe()">
                {{ 'team.empty.justMe' | transloco }}
              </button>
            }
            <button mat-stroked-button type="button" (click)="add()">
              {{ 'team.empty.addPerson' | transloco }}
            </button>
          </div>
        </section>
      } @else {
        <ul class="people">
          @for (person of team.value(); track person.id) {
            <li>
              <a class="person" [routerLink]="[person.id]" [class.inactive]="!person.isActive">
                <app-staff-avatar [name]="person.displayName" [color]="person.color" />
                <span class="text">
                  <span class="name">{{ person.displayName }}</span>
                  <span class="meta">
                    {{ accountKey(person) | transloco }}
                    @if (!person.isActive) {
                      · {{ 'team.status.inactive' | transloco }}
                    } @else if (!person.isBookableOnline) {
                      · {{ 'team.status.notOnline' | transloco }}
                    }
                  </span>
                </span>
                <mat-icon aria-hidden="true">chevron_right</mat-icon>
              </a>
            </li>
          }
        </ul>
      }
    } @else if (team.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class TeamPage {
  protected readonly team = resource({ loader: () => this.service.list() });
  protected readonly busy = signal(false);
  private readonly service = inject(TeamService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly errors = inject(ErrorNotifier);

  /** Owners and managers who do not have a calendar column of their own yet can take one. */
  protected readonly canLinkSelf = computed(() => this.auth.currentUser()?.staffId === null);

  protected accountKey(person: StaffMember): string {
    if (person.role) {
      return `team.role.${person.role}`;
    }
    return person.invitationPending ? 'team.status.invited' : 'team.status.noLogin';
  }

  protected async add(): Promise<void> {
    const data: StaffDialogData = {
      canLinkSelf: this.canLinkSelf(),
      canInviteManager: this.auth.currentUser()?.role === 'Owner',
    };
    const result = await firstValueFrom(
      this.dialog
        .open<StaffDialog, StaffDialogData, StaffDialogResult>(StaffDialog, {
          data,
          width: '520px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (!result) {
      return;
    }
    if (result.staff.isMe) {
      await this.auth.reload();
    }
    this.team.reload();
    this.toast.show('team.saved');
    if (result.inviteError) {
      this.errors.show(result.inviteError);
    }
  }

  protected async justMe(): Promise<void> {
    this.busy.set(true);
    try {
      await this.service.create({
        displayName: this.auth.currentUser()?.displayName ?? '',
        color: '#7C5CFC',
        isBookableOnline: true,
        autoConfirm: false,
        linkToCurrentUser: true,
      });
      await this.auth.reload();
      this.team.reload();
      this.toast.show('team.saved');
    } catch (error) {
      if (error instanceof ApiError) {
        this.errors.show(error);
      }
    } finally {
      this.busy.set(false);
    }
  }
}
