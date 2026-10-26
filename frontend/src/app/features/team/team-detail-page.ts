import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { InviteDialog, InviteDialogData } from './invite-dialog';
import { StaffAvatar } from './staff-avatar';
import { StaffDialog, StaffDialogData, StaffDialogResult } from './staff-dialog';
import { TeamService } from './team.service';

const TABS = [
  { path: 'services', labelKey: 'team.tabs.services' },
  { path: 'hours', labelKey: 'team.tabs.hours' },
  { path: 'absences', labelKey: 'team.tabs.absences' },
] as const;

/** One person: header with name, color and actions, and a routed tab for skills, working hours and absences. */
@Component({
  selector: 'app-team-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    StaffAvatar,
    TranslocoPipe,
  ],
  styleUrl: './team-detail-page.scss',
  template: `
    <a class="back" routerLink="/app/team">
      <mat-icon aria-hidden="true">arrow_back</mat-icon>
      {{ 'team.back' | transloco }}
    </a>

    @if (staff.hasValue()) {
      @let person = staff.value();
      <header class="head">
        <app-staff-avatar [name]="person.displayName" [color]="person.color" [size]="56" />
        <div class="title">
          <h1>{{ person.displayName }}</h1>
          <p class="meta">
            @if (person.role) {
              {{ 'team.role.' + person.role | transloco }}
            } @else if (person.invitationPending) {
              {{ 'team.status.invited' | transloco }}
            } @else {
              {{ 'team.status.noLogin' | transloco }}
            }
            @if (!person.isActive) {
              · {{ 'team.status.inactive' | transloco }}
            }
          </p>
        </div>
        <div class="actions">
          @if (!person.hasLogin) {
            <button mat-stroked-button type="button" (click)="invite()">
              {{
                (person.invitationPending ? 'team.invite.again' : 'team.invite.action') | transloco
              }}
            </button>
          }
          <button mat-stroked-button type="button" (click)="edit()">
            {{ 'team.edit' | transloco }}
          </button>
        </div>
      </header>

      <nav mat-tab-nav-bar [tabPanel]="panel" [attr.aria-label]="person.displayName">
        @for (tab of tabs; track tab.path) {
          <a
            mat-tab-link
            [routerLink]="tab.path"
            routerLinkActive
            #active="routerLinkActive"
            [active]="active.isActive"
          >
            {{ tab.labelKey | transloco }}
          </a>
        }
      </nav>
      <mat-tab-nav-panel #panel>
        <div class="panel"><router-outlet /></div>
      </mat-tab-nav-panel>
    } @else if (staff.error()) {
      <p class="alert" role="alert">{{ 'team.notFound' | transloco }}</p>
    } @else {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class TeamDetailPage {
  /** From the route (`team/:id`, bound by component input binding). */
  readonly id = input.required<string>();

  protected readonly tabs = TABS;
  protected readonly staff = resource({
    params: () => this.id(),
    loader: ({ params }) => this.service.get(params),
  });

  private readonly service = inject(TeamService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly errors = inject(ErrorNotifier);
  private readonly canInviteManager = computed(() => this.auth.currentUser()?.role === 'Owner');

  protected async edit(): Promise<void> {
    const data: StaffDialogData = {
      staff: this.staff.value(),
      canLinkSelf: false,
      canInviteManager: this.canInviteManager(),
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
    if (result) {
      this.staff.set(result.staff);
      this.toast.show('team.saved');
    }
  }

  protected async invite(): Promise<void> {
    const staff = this.staff.value();
    if (!staff) {
      return;
    }
    const sent = await firstValueFrom(
      this.dialog
        .open<InviteDialog, InviteDialogData, boolean>(InviteDialog, {
          data: { staff, canInviteManager: this.canInviteManager() },
          width: '480px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (sent) {
      this.staff.reload();
      this.toast.show('team.invite.sent');
    }
  }
}
