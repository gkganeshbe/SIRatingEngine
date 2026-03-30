import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { CoverageService } from '../../../core/services/coverage.service';
import { CoverageSummary } from '../../../core/models/api.models';
import { CoverageFormComponent } from '../coverage-form/coverage-form.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-coverage-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatChipsModule,
    MatTooltipModule, MatCardModule, MatProgressSpinnerModule, MatSelectModule,
  ],
  template: `
    <div class="page-container">
      <div class="action-bar">
        <h2 style="margin:0;flex:1">Coverage Configurations</h2>
        <mat-form-field style="width:220px" subscriptSizing="dynamic">
          <mat-label>Filter by product</mat-label>
          <input matInput [(ngModel)]="productFilter" (ngModelChange)="load()" placeholder="e.g. CONDO-IL">
        </mat-form-field>
        <button mat-flat-button color="primary" (click)="openCreate()">
          <mat-icon>add</mat-icon> New Coverage
        </button>
      </div>

      <mat-card>
        <mat-card-content>
          <div *ngIf="loading" style="text-align:center;padding:32px">
            <mat-spinner diameter="40" style="margin:auto"></mat-spinner>
          </div>

          <div *ngIf="error" style="text-align:center;padding:32px;color:#c62828">
            <mat-icon>error_outline</mat-icon>
            <p>{{error}}</p>
          </div>

          <table mat-table [dataSource]="coverages" *ngIf="!loading && !error" style="width:100%">
            <ng-container matColumnDef="productCode">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let c"><strong>{{c.productCode}}</strong></td>
            </ng-container>

            <ng-container matColumnDef="state">
              <th mat-header-cell *matHeaderCellDef>State</th>
              <td mat-cell *matCellDef="let c">
                <mat-chip>{{c.state}}</mat-chip>
              </td>
            </ng-container>

            <ng-container matColumnDef="coverageCode">
              <th mat-header-cell *matHeaderCellDef>Coverage</th>
              <td mat-cell *matCellDef="let c">{{c.coverageCode}}</td>
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
                <mat-chip *ngIf="c.expireAt" color="warn" highlighted>{{c.expireAt}}</mat-chip>
                <span *ngIf="!c.expireAt" style="color:rgba(0,0,0,.38)">—</span>
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let c" style="text-align:right">
                <button mat-icon-button matTooltip="Open pipeline & rate tables" color="primary"
                        (click)="openDetail(c)">
                  <mat-icon>tune</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Edit" (click)="openEdit(c)">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Expire" (click)="expire(c)"
                        [disabled]="!!c.expireAt">
                  <mat-icon>event_busy</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Delete" color="warn" (click)="delete(c)">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns;"
                [class.expired-row]="row.expireAt"
                style="cursor:pointer" (click)="openDetail(row)"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `
})
export class CoverageListComponent implements OnInit {
  columns = ['productCode', 'state', 'coverageCode', 'version', 'effStart', 'expireAt', 'actions'];
  coverages: CoverageSummary[] = [];
  loading = true;
  error = '';
  productFilter = '';

  constructor(
    private svc: CoverageService,
    private dialog: MatDialog,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.error = '';
    this.svc.list(this.productFilter || undefined).subscribe({
      next:  d  => { this.coverages = d; this.loading = false; this.cdr.detectChanges(); },
      error: (e) => { this.loading = false; this.error = e?.message ?? 'Failed to load coverages'; this.cdr.detectChanges(); }
    });
  }

  openDetail(c: CoverageSummary) {
    this.router.navigate(['/coverages', c.id], {
      queryParams: { pc: c.productCode, cc: c.coverageCode, v: c.version }
    });
  }

  openCreate() {
    this.dialog.open(CoverageFormComponent, { width: '560px', data: null })
      .afterClosed().subscribe(saved => { if (saved) this.load(); });
  }

  openEdit(c: CoverageSummary) {
    this.dialog.open(CoverageFormComponent, { width: '560px', data: c })
      .afterClosed().subscribe(saved => { if (saved) this.load(); });
  }

  expire(c: CoverageSummary) {
    const today = new Date().toISOString().slice(0, 10);
    this.svc.expire(c.id, today).subscribe(() => this.load());
  }

  delete(c: CoverageSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete Coverage',
        message: `Delete ${c.productCode} / ${c.coverageCode} v${c.version}?`
      }
    }).afterClosed().subscribe(ok => { if (ok) this.svc.delete(c.id).subscribe(() => this.load()); });
  }
}
