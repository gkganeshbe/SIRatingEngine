import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
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
  template: `
    <div class="page-container" *ngIf="coverage">
      <!-- Breadcrumb -->
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;color:rgba(0,0,0,.6)">
        <a routerLink="/products" style="color:inherit;text-decoration:none">Products</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <a [routerLink]="['/products', productId]"
           [queryParams]="{ pc: coverage.productCode, v: coverage.version }"
           style="color:inherit;text-decoration:none">{{coverage.productCode}}</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <span>{{coverage.coverageCode}} v{{coverage.version}}</span>
        <span *ngIf="coverage.state !== '*'" style="margin-left:4px">
          <mat-chip>{{coverage.state}}</mat-chip>
        </span>
      </div>

      <!-- Header card -->
      <mat-card style="margin-bottom:16px">
        <mat-card-content style="display:flex;align-items:center;gap:24px;flex-wrap:wrap">
          <div>
            <div style="font-size:11px;color:rgba(0,0,0,.6)">Eff Start</div>
            <strong>{{coverage.effStart}}</strong>
          </div>
          <div *ngIf="coverage.expireAt">
            <div style="font-size:11px;color:rgba(0,0,0,.6)">Expires</div>
            <strong style="color:#f44336">{{coverage.expireAt}}</strong>
          </div>
          <div>
            <div style="font-size:11px;color:rgba(0,0,0,.6)">Perils</div>
            <div class="peril-chips">
              <mat-chip *ngFor="let p of coverage.perils" color="primary" highlighted>{{p}}</mat-chip>
            </div>
          </div>
          <span class="spacer"></span>
          <div style="font-size:11px;color:rgba(0,0,0,.6);text-align:right">
            Created by {{coverage.createdBy ?? '—'}} on {{coverage.createdAt | date:'mediumDate'}}
          </div>
        </mat-card-content>
      </mat-card>

      <!-- Tabs -->
      <mat-tab-group animationDuration="150ms">

        <!-- ── Pipeline Tab ─────────────────────────────────────────────── -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:4px">account_tree</mat-icon>
            Pipeline
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

                  <!-- operation badge -->
                  <span [style.background]="opColor(step.operation)"
                        style="padding:2px 8px;border-radius:10px;font-size:11px;font-weight:500;color:white;white-space:nowrap">
                    {{step.operation}}
                  </span>

                  <!-- step id -->
                  <code style="font-size:12px;background:rgba(0,0,0,.06);padding:2px 6px;border-radius:4px">
                    {{step.id}}
                  </code>

                  <!-- step name -->
                  <span style="flex:1;font-size:13px">{{step.name}}</span>

                  <!-- rate table (lookup) -->
                  <span *ngIf="step.rateTable"
                        style="font-size:12px;color:rgba(0,0,0,.54)">
                    <mat-icon style="font-size:14px;vertical-align:middle">table_chart</mat-icon>
                    {{step.rateTable}}
                  </span>

                  <!-- when condition -->
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

        <!-- ── Rate Tables Tab ──────────────────────────────────────────── -->
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
                Rate tables store the factors used by pipeline lookup steps. Click a table name to view or edit its rows.
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

            <table mat-table [dataSource]="rateTables" *ngIf="!tablesLoading && rateTables.length > 0" style="width:100%">
              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef>Name</th>
                <td mat-cell *matCellDef="let t">
                  <a style="cursor:pointer;color:inherit;font-weight:500" (click)="openRateTable(t)">
                    {{t.name}}
                  </a>
                </td>
              </ng-container>
              <ng-container matColumnDef="lookupType">
                <th mat-header-cell *matHeaderCellDef>Lookup Type</th>
                <td mat-cell *matCellDef="let t">
                  <mat-chip>{{t.lookupType}}</mat-chip>
                </td>
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
                  <mat-chip *ngIf="t.expireAt" color="warn" highlighted>{{t.expireAt}}</mat-chip>
                  <span *ngIf="!t.expireAt" style="color:rgba(0,0,0,.38)">—</span>
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let t" style="text-align:right">
                  <button mat-icon-button matTooltip="Open rows" color="primary"
                          (click)="openRateTable(t)">
                    <mat-icon>open_in_new</mat-icon>
                  </button>
                  <button mat-icon-button matTooltip="Delete" color="warn"
                          (click)="deleteRateTable(t)">
                    <mat-icon>delete</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="tableColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: tableColumns;"
                  [class.expired-row]="row.expireAt"
                  style="cursor:pointer" (click)="openRateTable(row)"></tr>
            </table>
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
  coverage: CoverageDetail | null = null;
  steps: StepConfig[] = [];
  rateTables: RateTableSummary[] = [];
  loading = true;
  stepsLoading = false;
  tablesLoading = false;
  tableColumns = ['name', 'lookupType', 'description', 'effStart', 'expireAt', 'actions'];

  private coverageId = 0;
  productId = 0;   // used in breadcrumb to navigate back to product

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
    const qp = this.route.snapshot.queryParamMap;
    const pc = qp.get('pc')!;
    const cc = qp.get('cc')!;
    const v  = qp.get('v')!;

    this.coverageSvc.get(pc, cc, v).subscribe({
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
    this.dialog.open(StepFormComponent, { width: '640px', data: { step: null } })
      .afterClosed().subscribe((step: StepConfig | null) => {
        if (!step) return;
        this.pipelineSvc.addStep(this.coverageId, step).subscribe(() => this.loadSteps());
      });
  }

  editStep(step: StepConfig, _index: number) {
    this.dialog.open(StepFormComponent, { width: '640px', data: { step } })
      .afterClosed().subscribe((updated: StepConfig | null) => {
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
        // Revert on failure
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
