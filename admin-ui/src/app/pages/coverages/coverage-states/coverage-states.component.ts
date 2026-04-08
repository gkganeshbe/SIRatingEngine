import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CoverageService } from '../../../core/services/coverage.service';
import { CoverageSummary } from '../../../core/models/api.models';
import { isExpired } from '../../../core/utils/date.utils';
import { CoverageFormComponent } from '../coverage-form/coverage-form.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-coverage-states',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatButtonModule, MatIconModule, MatTableModule,
    MatChipsModule, MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
  ],
  template: `
    <div class="page-container">
      <!-- Breadcrumb -->
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;color:rgba(0,0,0,.6)">
        <a routerLink="/products" style="color:inherit;text-decoration:none">Products</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <a [routerLink]="['/products', productId]"
           [queryParams]="{ pc: productCode, v: version }"
           style="color:inherit;text-decoration:none">{{productCode}} v{{version}}</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <span>{{coverageCode}}</span>
      </div>

      <!-- Header -->
      <div class="action-bar" style="margin-bottom:12px">
        <div style="flex:1">
          <h2 style="margin:0">{{coverageCode}} &mdash; State Configurations</h2>
          <p style="margin:4px 0 0;color:rgba(0,0,0,.54);font-size:13px">
            Each row is an independent rating pipeline for a specific state (or * for all states).
            Click a row to configure pipeline steps and rate tables.
          </p>
        </div>
        <button mat-flat-button color="primary" (click)="addConfig()">
          <mat-icon>add</mat-icon> Add State Config
        </button>
      </div>

      <div *ngIf="loading" style="text-align:center;padding:48px">
        <mat-spinner diameter="36" style="margin:auto"></mat-spinner>
      </div>

      <mat-card *ngIf="!loading">
        <table mat-table [dataSource]="configs" style="width:100%">

          <ng-container matColumnDef="state">
            <th mat-header-cell *matHeaderCellDef>State</th>
            <td mat-cell *matCellDef="let c">
              <mat-chip>{{c.state === '*' ? 'All States (*)' : c.state}}</mat-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="version">
            <th mat-header-cell *matHeaderCellDef>Version</th>
            <td mat-cell *matCellDef="let c">{{c.version}}</td>
          </ng-container>

          <ng-container matColumnDef="effStart">
            <th mat-header-cell *matHeaderCellDef>Eff Start</th>
            <td mat-cell *matCellDef="let c">{{c.effStart}}</td>
          </ng-container>

          <ng-container matColumnDef="expireAt">
            <th mat-header-cell *matHeaderCellDef>Expires</th>
            <td mat-cell *matCellDef="let c">
              <mat-chip *ngIf="isExpired(c.expireAt)" color="warn" highlighted>{{c.expireAt}}</mat-chip>
              <span *ngIf="!isExpired(c.expireAt)" style="color:rgba(0,0,0,.38)">&mdash;</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let c" style="text-align:right">
              <button mat-icon-button matTooltip="Pipeline & Rate Tables" color="primary"
                      (click)="openPipeline(c); $event.stopPropagation()">
                <mat-icon>account_tree</mat-icon>
              </button>
              <button mat-icon-button matTooltip="Edit"
                      (click)="editConfig(c); $event.stopPropagation()">
                <mat-icon>edit</mat-icon>
              </button>
              <button mat-icon-button matTooltip="Expire"
                      [disabled]="isExpired(c.expireAt)"
                      (click)="expireConfig(c); $event.stopPropagation()">
                <mat-icon>event_busy</mat-icon>
              </button>
              <button mat-icon-button matTooltip="Delete" color="warn"
                      (click)="deleteConfig(c); $event.stopPropagation()">
                <mat-icon>delete</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"
              style="cursor:pointer" (click)="openPipeline(row)"
              [class.expired-row]="isExpired(row.expireAt)"></tr>

          <tr *matNoDataRow>
            <td [colSpan]="columns.length" style="padding:32px;text-align:center;color:#999">
              No state configurations yet. Click <strong>Add State Config</strong> to create one.
            </td>
          </tr>
        </table>
      </mat-card>
    </div>
  `
})
export class CoverageStatesComponent implements OnInit {
  readonly isExpired = isExpired;
  configs: CoverageSummary[] = [];
  loading = true;
  columns = ['state', 'version', 'effStart', 'expireAt', 'actions'];

  coverageRefId = 0;
  coverageCode  = '';
  productCode   = '';
  version       = '';
  productId     = 0;

  constructor(
    private route:       ActivatedRoute,
    private router:      Router,
    private coverageSvc: CoverageService,
    private dialog:      MatDialog,
    private cdr:         ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.coverageRefId = +this.route.snapshot.paramMap.get('coverageRefId')!;
    this.coverageCode  = this.route.snapshot.queryParamMap.get('code')      ?? '';
    this.productCode   = this.route.snapshot.queryParamMap.get('pc')        ?? '';
    this.version       = this.route.snapshot.queryParamMap.get('v')         ?? '';
    this.productId     = +(this.route.snapshot.queryParamMap.get('productId') ?? '0') || 0;
    this.load();
  }

  private load() {
    this.loading = true;
    this.coverageSvc.list(this.coverageRefId).subscribe({
      next:  d => { this.configs = d; this.loading = false; this.cdr.detectChanges(); },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  openPipeline(c: CoverageSummary) {
    this.router.navigate(['/coverages', c.id], {
      queryParams: { productId: this.productId }
    });
  }

  addConfig() {
    this.dialog.open(CoverageFormComponent, {
      width: '520px',
      data: { coverageRefId: this.coverageRefId, coverageCode: this.coverageCode }
    }).afterClosed().subscribe(saved => { if (saved) this.load(); });
  }

  editConfig(c: CoverageSummary) {
    this.dialog.open(CoverageFormComponent, {
      width: '520px',
      data: c
    }).afterClosed().subscribe(saved => { if (saved) this.load(); });
  }

  expireConfig(c: CoverageSummary) {
    const today = new Date().toISOString().slice(0, 10);
    this.coverageSvc.expire(c.id, today).subscribe(() => this.load());
  }

  deleteConfig(c: CoverageSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Config', message: `Delete ${c.state} v${c.version}?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.coverageSvc.delete(c.id).subscribe(() => this.load());
    });
  }
}
