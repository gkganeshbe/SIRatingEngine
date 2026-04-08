import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { isExpired } from '../../../core/utils/date.utils';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CoverageService } from '../../../core/services/coverage.service';
import { PipelineService } from '../../../core/services/pipeline.service';
import { RateTableService } from '../../../core/services/rate-table.service';
import { CoverageDetail, StepConfig, RateTableSummary } from '../../../core/models/api.models';
import { StepFormComponent } from '../../pipeline/step-form/step-form.component';
import { RateTableFormComponent } from '../../rate-tables/rate-table-form/rate-table-form.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-coverage-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule, DragDropModule,
    MatTabsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatChipsModule, MatCardModule, MatProgressSpinnerModule,
    MatDialogModule, MatTooltipModule, MatBadgeModule, MatSnackBarModule,
  ],
  styles: [`
    .detail-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 16px;
      padding: 16px 0;
    }
    .detail-block {
      background: #fafafa;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      padding: 12px 16px;
    }
    .detail-label {
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: rgba(0,0,0,.42);
      margin-bottom: 6px;
    }
    .agg-card {
      background: #f1f8e9;
      border: 1px solid #a5d6a7;
      border-radius: 6px;
      padding: 12px 16px;
      grid-column: 1 / -1;
    }
  `],
  template: `
    <div class="page-container" *ngIf="coverage">

      <!-- Slim header -->
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:8px;color:rgba(0,0,0,.6);flex-wrap:wrap">
        <a routerLink="/coverages" style="color:inherit;text-decoration:none">Coverage Mapping</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <span style="font-weight:600;color:rgba(0,0,0,.87)">
          {{coverage.productCode}} &mdash; {{coverage.coverageCode}}
        </span>
        <mat-chip>{{coverage.state === '*' ? 'All States' : coverage.state}}</mat-chip>
        <mat-chip>v{{coverage.version}}</mat-chip>
        <mat-chip *ngIf="isExpired(coverage.expireAt)"
                  style="background:#ffebee;color:#c62828;border:1px solid #ef9a9a">
          Expired {{coverage.expireAt}}
        </mat-chip>
        <mat-chip *ngIf="!isExpired(coverage.expireAt)"
                  style="background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7">
          Active
        </mat-chip>
      </div>

      <!-- Tab bar -->
      <mat-tab-group animationDuration="150ms" [selectedIndex]="initialTab">

        <!-- ── Tab 0: Coverage Details ─────────────────────────────────── -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:4px">info_outline</mat-icon>
            Coverage Details
          </ng-template>

          <div class="detail-grid">

            <!-- Effective dates -->
            <div class="detail-block">
              <div class="detail-label">Effective Dates</div>
              <div style="font-size:13px">
                <span style="color:rgba(0,0,0,.54);font-size:11px">From &nbsp;</span>
                <strong>{{coverage.effStart}}</strong>
              </div>
              <div *ngIf="isExpired(coverage.expireAt)" style="font-size:13px;margin-top:4px">
                <span style="color:rgba(0,0,0,.54);font-size:11px">Expires </span>
                <strong style="color:#c62828">{{coverage.expireAt}}</strong>
              </div>
              <div *ngIf="!isExpired(coverage.expireAt)" style="font-size:12px;color:rgba(0,0,0,.38);margin-top:4px">
                No expiry &mdash; Active
              </div>
            </div>

            <!-- Perils -->
            <div class="detail-block">
              <div class="detail-label">Perils</div>
              <div class="peril-chips" style="margin-top:4px">
                <mat-chip *ngFor="let p of coverage.perils" color="primary" highlighted>{{p}}</mat-chip>
                <span *ngIf="!coverage.perils.length" style="font-size:12px;color:rgba(0,0,0,.38)">None declared</span>
              </div>
            </div>

            <!-- Depends On -->
            <div class="detail-block" *ngIf="coverage.dependsOn.length">
              <div class="detail-label">Depends On</div>
              <div style="display:flex;gap:4px;flex-wrap:wrap;margin-top:4px">
                <mat-chip *ngFor="let d of coverage.dependsOn"
                          [matTooltip]="'Reads cov_' + d + '_Premium from the risk bag'">
                  {{d}}
                </mat-chip>
              </div>
            </div>

            <!-- Publishes -->
            <div class="detail-block" *ngIf="coverage.publish.length">
              <div class="detail-label">Publishes</div>
              <div style="display:flex;gap:4px;flex-wrap:wrap;margin-top:4px">
                <mat-chip *ngFor="let k of coverage.publish"
                          [matTooltip]="'Risk bag key exported for downstream: $risk.' + k">
                  {{k}}
                </mat-chip>
              </div>
            </div>

            <!-- Metadata -->
            <div class="detail-block">
              <div class="detail-label">Metadata</div>
              <div style="font-size:12px;color:rgba(0,0,0,.6);margin-top:4px">
                Created by <strong>{{coverage.createdBy ?? '—'}}</strong>
              </div>
              <div style="font-size:12px;color:rgba(0,0,0,.6);margin-top:2px">
                on {{coverage.createdAt | date:'mediumDate'}}
              </div>
            </div>

            <!-- Aggregate rules — full-width -->
            <div class="agg-card" *ngIf="coverage.aggregate">
              <div style="display:flex;align-items:center;gap:6px;font-weight:600;color:#2e7d32;margin-bottom:8px">
                <mat-icon style="font-size:18px;width:18px;height:18px;color:#2e7d32">merge_type</mat-icon>
                Aggregate Mode
              </div>
              <div style="font-size:13px;margin-bottom:8px">
                When
                <code style="background:rgba(0,0,0,.07);padding:2px 6px;border-radius:3px">
                  {{coverage.aggregate.whenPath}} {{coverage.aggregate.whenOp}} '{{coverage.aggregate.whenValue}}'
                </code>
              </div>
              <div style="display:flex;flex-wrap:wrap;gap:6px">
                <span *ngFor="let f of coverage.aggregate.fields"
                      style="background:#a5d6a7;color:#1b5e20;border-radius:10px;padding:2px 10px;font-size:12px"
                      [matTooltip]="f.aggFunction + '(' + f.sourceField + ') → $risk.' + f.resultKey">
                  {{f.aggFunction}}({{f.sourceField}}) → {{f.resultKey}}
                </span>
              </div>
            </div>

          </div>
        </mat-tab>

        <!-- ── Tab 1: Rate Tables ──────────────────────────────────────── -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:4px">table_chart</mat-icon>
            Rate Tables
            <span style="margin-left:6px;background:rgba(0,0,0,.12);border-radius:10px;padding:0 6px;font-size:11px">
              {{rateTables.length}}
            </span>
          </ng-template>

          <div style="padding:16px 0">
            <div class="action-bar">
              <span style="color:rgba(0,0,0,.6);font-size:13px">
                Rate tables store the factors used by pipeline lookup steps. Click a table to view rows or upload via Excel.
              </span>
              <span class="spacer"></span>
              <button mat-flat-button color="primary" (click)="addRateTable()">
                <mat-icon>add</mat-icon> New Rate Table
              </button>
            </div>

            <mat-card *ngIf="tablesLoading" style="text-align:center;padding:32px">
              <mat-spinner diameter="36" style="margin:auto"></mat-spinner>
            </mat-card>

            <div *ngIf="!tablesLoading && rateTables.length === 0"
                 style="color:rgba(0,0,0,.38);padding:48px;text-align:center">
              <mat-icon style="font-size:36px;width:36px;height:36px;margin-bottom:12px">table_chart</mat-icon>
              <p style="margin:0 0 4px;font-size:15px">No rate tables yet</p>
              <p style="margin:0;font-size:12px">Create rate tables to store the factors that pipeline lookup steps will reference.</p>
            </div>

            <table mat-table [dataSource]="rateTables"
                   *ngIf="!tablesLoading && rateTables.length > 0" style="width:100%">
              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef>Name</th>
                <td mat-cell *matCellDef="let t">
                  <span style="cursor:pointer;font-weight:500;color:#3f51b5"
                        (click)="openRateTable(t)">{{t.name}}</span>
                </td>
              </ng-container>
              <ng-container matColumnDef="lookupType">
                <th mat-header-cell *matHeaderCellDef>Lookup Type</th>
                <td mat-cell *matCellDef="let t"><mat-chip>{{t.lookupType}}</mat-chip></td>
              </ng-container>
              <ng-container matColumnDef="description">
                <th mat-header-cell *matHeaderCellDef>Description</th>
                <td mat-cell *matCellDef="let t">{{t.description ?? '—'}}</td>
              </ng-container>
              <ng-container matColumnDef="effStart">
                <th mat-header-cell *matHeaderCellDef>Eff Start</th>
                <td mat-cell *matCellDef="let t">{{t.effStart}}</td>
              </ng-container>
              <ng-container matColumnDef="expireAt">
                <th mat-header-cell *matHeaderCellDef>Expires</th>
                <td mat-cell *matCellDef="let t">
                  <mat-chip *ngIf="isExpired(t.expireAt)" color="warn" highlighted>{{t.expireAt}}</mat-chip>
                  <span *ngIf="!isExpired(t.expireAt)" style="color:rgba(0,0,0,.38)">—</span>
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let t" style="text-align:right">
                  <button mat-icon-button matTooltip="Open rows" color="primary"
                          (click)="openRateTable(t); $event.stopPropagation()">
                    <mat-icon>open_in_new</mat-icon>
                  </button>
                  <button mat-icon-button matTooltip="Delete" color="warn"
                          (click)="deleteRateTable(t); $event.stopPropagation()">
                    <mat-icon>delete</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="tableColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: tableColumns;"
                  [class.expired-row]="isExpired(row.expireAt)"
                  style="cursor:pointer" (click)="openRateTable(row)"></tr>
            </table>
          </div>
        </mat-tab>

        <!-- ── Tab 2: Rating Pipeline ──────────────────────────────────── -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:4px">account_tree</mat-icon>
            Rating Pipeline
            <span style="margin-left:6px;background:rgba(0,0,0,.12);border-radius:10px;padding:0 6px;font-size:11px">
              {{steps.length}}
            </span>
          </ng-template>

          <div style="padding:16px 0">
            <div class="action-bar">
              <span style="color:rgba(0,0,0,.6);font-size:13px">
                Steps execute in order to calculate the premium. Drag rows to reorder — saves automatically.
              </span>
              <span class="spacer"></span>
              <button mat-flat-button color="primary" (click)="addStep()">
                <mat-icon>add</mat-icon> Add Step
              </button>
            </div>

            <mat-card *ngIf="stepsLoading" style="text-align:center;padding:32px">
              <mat-spinner diameter="36" style="margin:auto"></mat-spinner>
            </mat-card>

            <div *ngIf="!stepsLoading && steps.length === 0"
                 style="color:rgba(0,0,0,.38);padding:48px;text-align:center">
              <mat-icon style="font-size:36px;width:36px;height:36px;margin-bottom:12px">account_tree</mat-icon>
              <p style="margin:0 0 4px;font-size:15px">No pipeline steps yet</p>
              <p style="margin:0;font-size:12px">Add steps to define how the premium is calculated —
                lookups against rate tables, computations, and rounding.</p>
            </div>

            <div cdkDropList class="step-list" (cdkDropListDropped)="onDrop($event)" *ngIf="!stepsLoading">
              <mat-card *ngFor="let step of steps; let i = index"
                        cdkDrag class="step-row" style="margin-bottom:6px">
                <mat-card-content style="display:flex;align-items:center;gap:12px;padding:8px 16px">
                  <mat-icon cdkDragHandle class="drag-handle">drag_indicator</mat-icon>

                  <span style="width:28px;color:rgba(0,0,0,.38);font-size:12px;text-align:right">
                    {{i + 1}}
                  </span>

                  <span [style.background]="opColor(step.operation)"
                        style="padding:2px 8px;border-radius:10px;font-size:11px;font-weight:500;color:white;white-space:nowrap">
                    {{step.operation}}
                  </span>

                  <code style="font-size:12px;background:rgba(0,0,0,.06);padding:2px 6px;border-radius:4px">
                    {{step.id}}
                  </code>

                  <span style="flex:1;font-size:13px">{{step.name}}</span>

                  <span *ngIf="step.rateTable" style="font-size:12px;color:rgba(0,0,0,.54)">
                    <mat-icon style="font-size:14px;vertical-align:middle">table_chart</mat-icon>
                    {{step.rateTable}}
                  </span>

                  <mat-icon *ngIf="step.when?.path" matTooltip="Has condition guard"
                            style="font-size:16px;color:#ff9800">filter_alt</mat-icon>

                  <button mat-icon-button (click)="editStep(step, i)" matTooltip="Edit step">
                    <mat-icon>edit</mat-icon>
                  </button>
                  <button mat-icon-button color="warn" (click)="deleteStep(step)" matTooltip="Delete step">
                    <mat-icon>delete</mat-icon>
                  </button>
                </mat-card-content>
              </mat-card>
            </div>
          </div>
        </mat-tab>

      </mat-tab-group>
    </div>

    <div *ngIf="loading" style="text-align:center;padding:64px">
      <mat-spinner diameter="48" style="margin:auto"></mat-spinner>
    </div>
  `
})
export class CoverageDetailComponent implements OnInit {
  readonly isExpired = isExpired;
  coverage: CoverageDetail | null = null;
  steps: StepConfig[] = [];
  rateTables: RateTableSummary[] = [];
  loading = true;
  stepsLoading = false;
  tablesLoading = false;
  tableColumns = ['name', 'lookupType', 'description', 'effStart', 'expireAt', 'actions'];

  private coverageId = 0;
  productId = 0;
  /** 0 = Coverage Details, 1 = Rate Tables, 2 = Rating Pipeline */
  initialTab = 0;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private coverageSvc: CoverageService,
    private pipelineSvc: PipelineService,
    private rateTableSvc: RateTableService,
    private dialog: MatDialog,
    private snack: MatSnackBar,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.coverageId = +this.route.snapshot.paramMap.get('id')!;
    this.productId  = +(this.route.snapshot.queryParamMap.get('productId') ?? 0);
    this.initialTab = +(this.route.snapshot.queryParamMap.get('tab') ?? 0);

    this.coverageSvc.get(this.coverageId).subscribe({
      next: d => {
        this.coverage = d;
        this.loading = false;
        this.cdr.detectChanges();
        this.loadSteps();
        this.loadRateTables();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadSteps() {
    this.stepsLoading = true;
    this.pipelineSvc.listSteps(this.coverageId).subscribe({
      next:  s  => { this.steps = s; this.stepsLoading = false; this.cdr.detectChanges(); },
      error: () => { this.stepsLoading = false; this.cdr.detectChanges(); }
    });
  }

  loadRateTables() {
    this.tablesLoading = true;
    this.rateTableSvc.list(this.coverageId).subscribe({
      next:  t  => { this.rateTables = t; this.tablesLoading = false; this.cdr.detectChanges(); },
      error: () => { this.tablesLoading = false; this.cdr.detectChanges(); }
    });
  }

  // ── Pipeline ──────────────────────────────────────────────────────────────

  addStep() {
    this.dialog.open(StepFormComponent, {
      width: '640px',
      data: { step: null, productCode: this.coverage?.productCode ?? '' }
    }).afterClosed().subscribe((step: StepConfig | null) => {
      if (!step) return;
      this.pipelineSvc.addStep(this.coverageId, step).subscribe(() => this.loadSteps());
    });
  }

  editStep(step: StepConfig, _index: number) {
    this.dialog.open(StepFormComponent, {
      width: '640px',
      data: { step, productCode: this.coverage?.productCode ?? '' }
    }).afterClosed().subscribe((updated: StepConfig | null) => {
      if (!updated) return;
      this.pipelineSvc.updateStep(this.coverageId, step.id, updated).subscribe(() => this.loadSteps());
    });
  }

  deleteStep(step: StepConfig) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Step', message: `Delete step "${step.id}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.pipelineSvc.deleteStep(this.coverageId, step.id).subscribe(() => this.loadSteps());
    });
  }

  onDrop(event: CdkDragDrop<StepConfig[]>) {
    moveItemInArray(this.steps, event.previousIndex, event.currentIndex);
    const ordered = this.steps.map(s => s.id);
    this.pipelineSvc.reorderSteps(this.coverageId, ordered).subscribe({
      next:  () => this.snack.open('Order saved', '', { duration: 2000 }),
      error: () => {
        moveItemInArray(this.steps, event.currentIndex, event.previousIndex);
        this.snack.open('Reorder failed', 'Dismiss', { duration: 3000 });
      }
    });
  }

  // ── Rate Tables ───────────────────────────────────────────────────────────

  addRateTable() {
    this.dialog.open(RateTableFormComponent, {
      width: '600px',
      data: { coverageId: this.coverageId, table: null }
    }).afterClosed().subscribe(saved => { if (saved) this.loadRateTables(); });
  }

  openRateTable(t: RateTableSummary) {
    this.router.navigate(['/rate-tables', this.coverageId, t.name]);
  }

  deleteRateTable(t: RateTableSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Rate Table', message: `Delete rate table "${t.name}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.rateTableSvc.delete(this.coverageId, t.id).subscribe(() => this.loadRateTables());
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  opColor(op: string): string {
    const map: Record<string, string> = {
      lookup:  '#1976d2',
      compute: '#388e3c',
      round:   '#f57c00',
    };
    return map[op] ?? '#757575';
  }
}
