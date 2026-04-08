import { Component, OnInit } from '@angular/core';
import { RouterModule, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from './core/services/auth.service';
import { TenantService } from './core/services/tenant.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterModule, FormsModule,
    MatToolbarModule, MatSidenavModule, MatListModule,
    MatIconModule, MatButtonModule, MatMenuModule,
    MatSelectModule, MatTooltipModule,
  ],
  template: `
    <mat-sidenav-container style="height:100%">
      <mat-sidenav mode="side" opened style="width:220px">
        <mat-toolbar color="primary" style="height:64px">
          <span style="font-size:15px;font-weight:500;line-height:1.2">Rating Engine<br><small style="font-weight:300">Admin Portal</small></span>
        </mat-toolbar>
        <mat-nav-list>
          <a mat-list-item routerLink="/products" routerLinkActive="active-link">
            <mat-icon matListItemIcon>inventory_2</mat-icon>
            <span matListItemTitle>Products</span>
          </a>
          <a mat-list-item routerLink="/coverages" [queryParams]="{}" routerLinkActive="active-link"
             [routerLinkActiveOptions]="{exact: false}">
            <mat-icon matListItemIcon>map</mat-icon>
            <span matListItemTitle>Coverage Mapping</span>
          </a>
          <a mat-list-item routerLink="/risk-fields" routerLinkActive="active-link">
            <mat-icon matListItemIcon>tune</mat-icon>
            <span matListItemTitle>Risk Field Mapping</span>
          </a>
          <a mat-list-item routerLink="/lookups" routerLinkActive="active-link">
            <mat-icon matListItemIcon>list_alt</mat-icon>
            <span matListItemTitle>Lookups &amp; Keys</span>
          </a>
          <a mat-list-item routerLink="/test-rating" routerLinkActive="active-link">
            <mat-icon matListItemIcon>science</mat-icon>
            <span matListItemTitle>Validation &amp; Testing</span>
          </a>
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar color="primary" style="position:sticky;top:0;z-index:100">
          <span class="spacer"></span>

          <!-- Tenant selector -->
          <mat-select
            [(ngModel)]="selectedTenantId"
            (ngModelChange)="onTenantChange($event)"
            style="width:160px;color:white;margin-right:16px"
            matTooltip="Select tenant">
            <mat-option *ngFor="let t of tenants" [value]="t.id">{{t.name}}</mat-option>
          </mat-select>

          <!-- User menu -->
          <button mat-icon-button [matMenuTriggerFor]="userMenu">
            <mat-icon>account_circle</mat-icon>
          </button>
          <mat-menu #userMenu="matMenu">
            <button mat-menu-item disabled>
              <mat-icon>person</mat-icon>
              <span>{{userName}}</span>
            </button>
            <mat-divider></mat-divider>
            <button mat-menu-item (click)="auth.logout()">
              <mat-icon>logout</mat-icon>
              <span>Sign out</span>
            </button>
          </mat-menu>
        </mat-toolbar>

        <div style="min-height:calc(100vh - 64px)">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .active-link { background: rgba(0,0,0,.08); }
    mat-nav-list a { border-radius: 0 24px 24px 0; margin-right: 8px; }
    ::ng-deep .mat-mdc-select-value { color: white !important; }
    ::ng-deep .mat-mdc-select-arrow { color: white !important; }
  `]
})
export class AppComponent implements OnInit {
  tenants = this.tenantService.tenants;
  selectedTenantId = this.tenantService.tenantId;
  userName = '';

  constructor(
    public auth: AuthService,
    private tenantService: TenantService
  ) {}

  ngOnInit() {
    this.auth.init();
    this.auth.userName$.subscribe(n => this.userName = n);
  }

  onTenantChange(id: string) {
    this.tenantService.setTenantId(id);
  }
}
