import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { RateTableService } from '../../../core/services/rate-table.service';
import { ColumnDefService } from '../../../core/services/column-def.service';
import {
  RateTableDetail, RateTableRowDetail, ColumnDefDetail,
  CreateRateTableRowRequest, ColumnDefRequest
} from '../../../core/models/api.models';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-rate-table-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, RouterModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatTabsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatCheckboxModule, MatChipsModule, MatProgressSpinnerModule,
    MatTooltipModule, MatSnackBarModule, MatDialogModule,
  ],
  template: `
    <div class="page-container" *ngIf="table">
      <!-- Breadcrumb -->
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;color:rgba(0,0,0,.6)">
        <a [routerLink]="['/coverages', coverageId]"
           [queryParams]="coverageQueryParams"
           style="color:inherit;text-decoration:none">← Coverage</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <span>{{table.name}}</span>
        <mat-chip>{{table.lookupType}}</mat-chip>
      </div>

      <mat-tab-group animationDuration="150ms">

        <!-- ── Rows Tab ────────────────────────────────────────────────── -->
        <mat-tab label="Rate Rows">
          <div style="padding:16px 0">

            <div class="action-bar">
              <mat-form-field style="width:180px" subscriptSizing="dynamic">
                <mat-label>Eff Date filter</mat-label>
                <input matInput [(ngModel)]="effectiveDateFilter" placeholder="YYYY-MM-DD"
                       (ngModelChange)="loadRows()">
              </mat-form-field>
              <span class="spacer"></span>
              <button mat-stroked-button (click)="showImport = !showImport">
                <mat-icon>upload</mat-icon> Bulk Import
              </button>
              <button mat-flat-button color="primary" (click)="startAddRow()">
                <mat-icon>add</mat-icon> Add Row
              </button>
            </div>

            <!-- Bulk import panel -->
            <mat-card *ngIf="showImport" style="margin-bottom:16px">
              <mat-card-header><mat-card-title>Bulk Import (JSON)</mat-card-title></mat-card-header>
              <mat-card-content>
                <p style="font-size:12px;color:rgba(0,0,0,.6)">
                  Paste a JSON array of row objects. Each object maps column names to values.
                  Key columns: Key1-Key5. Value columns: Factor, Additive, AdditionalUnit, AdditionalRate.
                  Required: effStart (YYYY-MM-DD).
                </p>
                <mat-form-field class="full-width">
                  <mat-label>JSON rows</mat-label>
                  <textarea matInput [(ngModel)]="bulkJson" rows="6"
                            placeholder='[{"Key1":"FR","Key2":"1","Factor":0.95,"effStart":"2026-02-01"}]'>
                  </textarea>
                </mat-form-field>
              </mat-card-content>
              <mat-card-actions>
                <button mat-flat-button color="primary" (click)="bulkImport()" [disabled]="!bulkJson">
                  Import
                </button>
                <button mat-button (click)="showImport = false">Cancel</button>
              </mat-card-actions>
            </mat-card>

            <!-- Add/Edit row inline form -->
            <mat-card *ngIf="editingRow" style="margin-bottom:16px">
              <mat-card-header>
                <mat-card-title>{{editRowId ? 'Edit Row' : 'New Row'}}</mat-card-title>
              </mat-card-header>
              <mat-card-content>
                <form [formGroup]="rowForm" class="dialog-form">
                  <div class="form-row" style="flex-wrap:wrap">
                    <mat-form-field *ngFor="let col of keyColumns">
                      <mat-label>{{colLabel(col)}}</mat-label>
                      <input matInput [formControlName]="col.toLowerCase()">
                    </mat-form-field>
                  </div>
                  <div class="form-row" *ngIf="hasRange">
                    <mat-form-field>
                      <mat-label>Range From</mat-label>
                      <input matInput type="number" formControlName="rangeFrom">
                    </mat-form-field>
                    <mat-form-field>
                      <mat-label>Range To</mat-label>
                      <input matInput type="number" formControlName="rangeTo">
                    </mat-form-field>
                  </div>
                  <div class="form-row" style="flex-wrap:wrap">
                    <mat-form-field *ngFor="let col of valueColumns">
                      <mat-label>{{colLabel(col)}}</mat-label>
                      <input matInput type="number" [formControlName]="col.toLowerCase()">
                    </mat-form-field>
                  </div>
                  <div class="form-row">
                    <mat-form-field>
                      <mat-label>Eff Start</mat-label>
                      <input matInput formControlName="effStart" placeholder="YYYY-MM-DD">
                    </mat-form-field>
                    <mat-form-field>
                      <mat-label>Expire At (optional)</mat-label>
                      <input matInput formControlName="expireAt" placeholder="YYYY-MM-DD">
                    </mat-form-field>
                  </div>
                </form>
              </mat-card-content>
              <mat-card-actions>
                <button mat-flat-button color="primary" (click)="saveRow()" [disabled]="rowForm.invalid">
                  {{editRowId ? 'Update' : 'Add'}}
                </button>
                <button mat-button (click)="cancelEdit()">Cancel</button>
              </mat-card-actions>
            </mat-card>

            <!-- Rows table -->
            <div *ngIf="rowsLoading" style="text-align:center;padding:32px">
              <mat-spinner diameter="36" style="margin:auto"></mat-spinner>
            </div>

            <div *ngIf="!rowsLoading" style="overflow-x:auto">
              <table mat-table [dataSource]="rows" style="min-width:800px;width:100%">
                <ng-container *ngFor="let col of allDisplayColumns" [matColumnDef]="col">
                  <th mat-header-cell *matHeaderCellDef>{{colLabel(col)}}</th>
                  <td mat-cell *matCellDef="let row">
                    {{getRowValue(row, col)}}
                  </td>
                </ng-container>
                <ng-container matColumnDef="effStart">
                  <th mat-header-cell *matHeaderCellDef>Eff Start</th>
                  <td mat-cell *matCellDef="let row">{{row.effStart}}</td>
                </ng-container>
                <ng-container matColumnDef="expireAt">
                  <th mat-header-cell *matHeaderCellDef>Expires</th>
                  <td mat-cell *matCellDef="let row">
                    <span *ngIf="row.expireAt" style="color:#f44336">{{row.expireAt}}</span>
                    <span *ngIf="!row.expireAt" style="color:rgba(0,0,0,.38)">—</span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="rowActions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let row" style="text-align:right;white-space:nowrap">
                    <button mat-icon-button matTooltip="Edit" (click)="startEditRow(row)">
                      <mat-icon>edit</mat-icon>
                    </button>
                    <button mat-icon-button matTooltip="Expire" [disabled]="!!row.expireAt"
                            (click)="expireRow(row)">
                      <mat-icon>event_busy</mat-icon>
                    </button>
                    <button mat-icon-button matTooltip="Delete" color="warn"
                            (click)="deleteRow(row)">
                      <mat-icon>delete</mat-icon>
                    </button>
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="tableRowColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: tableRowColumns;"
                    [class.expired-row]="row.expireAt"></tr>
              </table>
              <div *ngIf="rows.length === 0" style="color:rgba(0,0,0,.38);padding:24px;text-align:center">
                No rows found. Add a row or adjust the effective date filter.
              </div>
            </div>
          </div>
        </mat-tab>

        <!-- ── Column Defs Tab ─────────────────────────────────────────── -->
        <mat-tab label="Column Definitions">
          <div style="padding:16px 0">
            <div class="action-bar">
              <span class="spacer"></span>
              <button mat-flat-button color="primary" (click)="startAddColDef()">
                <mat-icon>add</mat-icon> Add Column
              </button>
            </div>

            <table mat-table [dataSource]="colDefs" style="width:100%">
              <ng-container matColumnDef="columnName">
                <th mat-header-cell *matHeaderCellDef>Column</th>
                <td mat-cell *matCellDef="let c">{{c.columnName}}</td>
              </ng-container>
              <ng-container matColumnDef="displayLabel">
                <th mat-header-cell *matHeaderCellDef>Display Label</th>
                <td mat-cell *matCellDef="let c">{{c.displayLabel}}</td>
              </ng-container>
              <ng-container matColumnDef="dataType">
                <th mat-header-cell *matHeaderCellDef>Data Type</th>
                <td mat-cell *matCellDef="let c"><mat-chip>{{c.dataType}}</mat-chip></td>
              </ng-container>
              <ng-container matColumnDef="sortOrder">
                <th mat-header-cell *matHeaderCellDef>Order</th>
                <td mat-cell *matCellDef="let c">{{c.sortOrder}}</td>
              </ng-container>
              <ng-container matColumnDef="isRequired">
                <th mat-header-cell *matHeaderCellDef>Required</th>
                <td mat-cell *matCellDef="let c">
                  <mat-icon *ngIf="c.isRequired" color="primary">check_circle</mat-icon>
                  <mat-icon *ngIf="!c.isRequired" style="color:rgba(0,0,0,.2)">radio_button_unchecked</mat-icon>
                </td>
              </ng-container>
              <ng-container matColumnDef="colActions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let c" style="text-align:right">
                  <button mat-icon-button matTooltip="Delete" color="warn"
                          (click)="deleteColDef(c)">
                    <mat-icon>delete</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="colDefColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: colDefColumns;"></tr>
            </table>
          </div>
        </mat-tab>

        <!-- ── Metadata Tab ────────────────────────────────────────────── -->
        <mat-tab label="Metadata">
          <div style="padding:16px 0;max-width:600px">
            <mat-card>
              <mat-card-content>
                <table style="width:100%;border-collapse:collapse">
                  <tr *ngFor="let row of metaRows">
                    <td style="padding:8px;color:rgba(0,0,0,.6);width:160px;font-size:13px">{{row.label}}</td>
                    <td style="padding:8px;font-size:13px">{{row.value}}</td>
                  </tr>
                </table>
              </mat-card-content>
            </mat-card>
          </div>
        </mat-tab>

      </mat-tab-group>
    </div>

    <div *ngIf="loading" style="text-align:center;padding:64px">
      <mat-spinner diameter="48" style="margin:auto"></mat-spinner>
    </div>
  `
})
export class RateTableDetailComponent implements OnInit {
  table: RateTableDetail | null = null;
  rows: RateTableRowDetail[] = [];
  colDefs: ColumnDefDetail[] = [];
  loading = true;
  rowsLoading = false;
  coverageId = 0;
  tableName = '';
  effectiveDateFilter = '';
  showImport = false;
  bulkJson = '';
  coverageQueryParams: Record<string, string> = {};

  // Row editing
  editingRow = false;
  editRowId: number | null = null;
  rowForm!: FormGroup;

  colDefColumns = ['columnName','displayLabel','dataType','sortOrder','isRequired','colActions'];

  constructor(
    private route: ActivatedRoute,
    private svc: RateTableService,
    private colDefSvc: ColumnDefService,
    private dialog: MatDialog,
    private snack: MatSnackBar,
    private fb: FormBuilder
  ) {}

  ngOnInit() {
    this.coverageId = +this.route.snapshot.paramMap.get('coverageId')!;
    this.tableName  = this.route.snapshot.paramMap.get('name')!;

    // Store coverage nav params for breadcrumb
    const nav = window.history.state;
    this.coverageQueryParams = nav?.coverageQueryParams ?? {};

    this.svc.get(this.coverageId, this.tableName).subscribe({
      next: t => {
        this.table = t;
        this.colDefs = t.columnDefs;
        this.loading = false;
        this.loadRows();
      },
      error: () => { this.loading = false; }
    });
  }

  loadRows() {
    if (!this.table) return;
    this.rowsLoading = true;
    this.svc.getRows(this.coverageId, this.tableName, this.effectiveDateFilter || undefined).subscribe({
      next:  r  => { this.rows = r; this.rowsLoading = false; },
      error: () => { this.rowsLoading = false; }
    });
  }

  // ── Column helpers ────────────────────────────────────────────────────────

  get keyColumns(): string[] {
    return this.colDefs
      .filter(c => c.columnName.startsWith('Key'))
      .map(c => c.columnName)
      .sort();
  }

  get hasRange(): boolean {
    return this.colDefs.some(c => c.columnName === 'RangeFrom' || c.columnName === 'RangeTo');
  }

  get valueColumns(): string[] {
    return this.colDefs
      .filter(c => ['Factor','Additive','AdditionalUnit','AdditionalRate'].includes(c.columnName))
      .map(c => c.columnName);
  }

  get allDisplayColumns(): string[] {
    return [...this.keyColumns,
            ...(this.hasRange ? ['RangeFrom','RangeTo'] : []),
            ...this.valueColumns];
  }

  get tableRowColumns(): string[] {
    return [...this.allDisplayColumns, 'effStart', 'expireAt', 'rowActions'];
  }

  get metaRows() {
    if (!this.table) return [];
    return [
      { label: 'Table Name',    value: this.table.name },
      { label: 'Lookup Type',   value: this.table.lookupType },
      { label: 'Description',   value: this.table.description ?? '—' },
      { label: 'Interp. Key',   value: this.table.interpolationKeyCol ?? '—' },
      { label: 'Eff Start',     value: this.table.effStart },
      { label: 'Expires',       value: this.table.expireAt ?? '—' },
      { label: 'Created By',    value: this.table.createdBy ?? '—' },
    ];
  }

  colLabel(col: string): string {
    const def = this.colDefs.find(c => c.columnName === col);
    return def?.displayLabel ?? col;
  }

  getRowValue(row: RateTableRowDetail, col: string): string {
    const key = col.charAt(0).toLowerCase() + col.slice(1) as keyof RateTableRowDetail;
    const val = row[key];
    return val !== null && val !== undefined ? String(val) : '—';
  }

  // ── Row CRUD ──────────────────────────────────────────────────────────────

  private buildRowForm(row?: RateTableRowDetail) {
    const controls: Record<string, unknown> = {
      effStart: [row?.effStart ?? '', Validators.required],
      expireAt: [row?.expireAt ?? ''],
    };
    ['key1','key2','key3','key4','key5'].forEach(k => {
      controls[k] = [(row as unknown as Record<string,unknown>)?.[k] ?? ''];
    });
    ['rangeFrom','rangeTo','factor','additive','additionalUnit','additionalRate'].forEach(k => {
      controls[k] = [(row as unknown as Record<string,unknown>)?.[k] ?? ''];
    });
    this.rowForm = this.fb.group(controls);
  }

  startAddRow() {
    this.editRowId = null;
    this.buildRowForm();
    this.editingRow = true;
  }

  startEditRow(row: RateTableRowDetail) {
    this.editRowId = row.id;
    this.buildRowForm(row);
    this.editingRow = true;
  }

  cancelEdit() { this.editingRow = false; this.editRowId = null; }

  saveRow() {
    if (this.rowForm.invalid) return;
    const v = this.rowForm.getRawValue();
    const req: CreateRateTableRowRequest = {
      key1: v.key1 || null, key2: v.key2 || null, key3: v.key3 || null,
      key4: v.key4 || null, key5: v.key5 || null,
      rangeFrom: v.rangeFrom !== '' ? +v.rangeFrom : null,
      rangeTo:   v.rangeTo   !== '' ? +v.rangeTo   : null,
      factor:    v.factor    !== '' ? +v.factor    : null,
      additive:  v.additive  !== '' ? +v.additive  : null,
      additionalUnit: v.additionalUnit !== '' ? +v.additionalUnit : null,
      additionalRate: v.additionalRate !== '' ? +v.additionalRate : null,
      effStart:  v.effStart,
      expireAt:  v.expireAt || null,
    };

    const done = () => { this.cancelEdit(); this.loadRows(); };

    if (this.editRowId) {
      this.svc.updateRow(this.coverageId, this.tableName, this.editRowId, req)
        .subscribe({ next: done, error: () => this.snack.open('Update failed', 'Dismiss', { duration: 3000 }) });
    } else {
      this.svc.addRow(this.coverageId, this.tableName, req)
        .subscribe({ next: done, error: () => this.snack.open('Add failed', 'Dismiss', { duration: 3000 }) });
    }
  }

  expireRow(row: RateTableRowDetail) {
    const today = new Date().toISOString().slice(0, 10);
    this.svc.expireRow(this.coverageId, this.tableName, row.id, today)
      .subscribe(() => this.loadRows());
  }

  deleteRow(row: RateTableRowDetail) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Row', message: 'Delete this rate row? This cannot be undone.' }
    }).afterClosed().subscribe(ok => {
      if (ok) this.svc.deleteRow(this.coverageId, this.tableName, row.id).subscribe(() => this.loadRows());
    });
  }

  // ── Bulk import ───────────────────────────────────────────────────────────

  bulkImport() {
    let rows: Record<string, unknown>[];
    try { rows = JSON.parse(this.bulkJson); }
    catch { this.snack.open('Invalid JSON', 'Dismiss', { duration: 3000 }); return; }

    const requests: CreateRateTableRowRequest[] = rows.map(r => ({
      key1: (r['Key1'] as string) ?? null,
      key2: (r['Key2'] as string) ?? null,
      key3: (r['Key3'] as string) ?? null,
      key4: (r['Key4'] as string) ?? null,
      key5: (r['Key5'] as string) ?? null,
      rangeFrom: r['RangeFrom'] != null ? +(r['RangeFrom'] as number) : null,
      rangeTo:   r['RangeTo']   != null ? +(r['RangeTo']   as number) : null,
      factor:    r['Factor']    != null ? +(r['Factor']    as number) : null,
      additive:  r['Additive']  != null ? +(r['Additive']  as number) : null,
      additionalUnit: r['AdditionalUnit'] != null ? +(r['AdditionalUnit'] as number) : null,
      additionalRate: r['AdditionalRate'] != null ? +(r['AdditionalRate'] as number) : null,
      effStart:  (r['effStart'] as string) ?? (r['EffStart'] as string),
      expireAt:  (r['expireAt'] as string) ?? (r['ExpireAt'] as string) ?? null,
    }));

    this.svc.bulkInsertRows(this.coverageId, this.tableName, requests).subscribe({
      next: res => {
        this.snack.open(`Imported ${res.inserted} rows`, '', { duration: 3000 });
        this.showImport = false;
        this.bulkJson   = '';
        this.loadRows();
      },
      error: () => this.snack.open('Bulk import failed', 'Dismiss', { duration: 4000 })
    });
  }

  // ── Column Defs ───────────────────────────────────────────────────────────

  startAddColDef() {
    // Simple: open a mini dialog (inline prompt for now)
    const columnName  = window.prompt('Column name (Key1-Key5, Factor, Additive, etc.):');
    if (!columnName) return;
    const displayLabel = window.prompt('Display label:', columnName) ?? columnName;
    const newDef: ColumnDefRequest = {
      columnName, displayLabel, dataType: 'string',
      sortOrder: this.colDefs.length, isRequired: false
    };
    const updated = [
      ...this.colDefs.map(c => ({
        columnName: c.columnName, displayLabel: c.displayLabel,
        dataType: c.dataType, sortOrder: c.sortOrder, isRequired: c.isRequired
      })),
      newDef
    ];
    this.colDefSvc.replace(this.tableName, updated).subscribe(() => this.reloadColDefs());
  }

  deleteColDef(c: ColumnDefDetail) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Column', message: `Remove column definition "${c.columnName}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.colDefSvc.delete(this.tableName, c.id).subscribe(() => this.reloadColDefs());
    });
  }

  private reloadColDefs() {
    this.colDefSvc.list(this.tableName).subscribe(d => { this.colDefs = d; });
  }
}
