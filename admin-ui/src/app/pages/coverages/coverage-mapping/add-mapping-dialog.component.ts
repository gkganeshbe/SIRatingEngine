import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { CoverageService } from '../../../core/services/coverage.service';
import { CoverageRefDetail, LobRefDetail, ProductStateDetail } from '../../../core/models/api.models';
import { ACTIVE_EXPIRE } from '../../../core/utils/date.utils';

export interface AddMappingDialogData {
  /** LOBs with coverages (commercial). Empty for personal lines. */
  lobs: LobRefDetail[];
  /** Top-level coverages (personal lines or unassigned). */
  coverages: CoverageRefDetail[];
  /** Declared supported states for this product. */
  productStates: ProductStateDetail[];
  /** Pre-select a specific coverage ref (from "Add State Config" row button). */
  preselectedCoverageRefId?: number;
}

@Component({
  selector: 'app-add-mapping-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatSelectModule, MatInputModule,
    MatButtonModule, MatIconModule, MatDividerModule,
  ],
  template: `
    <h2 mat-dialog-title style="display:flex;align-items:center;gap:8px">
      <mat-icon color="primary">add_circle</mat-icon>
      New Coverage Mapping
    </h2>

    <mat-dialog-content style="min-width:440px;padding-top:8px">
      <p style="margin:0 0 20px;font-size:13px;color:rgba(0,0,0,.54)">
        Select a coverage from the product catalog and the state to create a rating configuration for.
      </p>

      <form [formGroup]="form">

        <!-- Coverage selector -->
        <mat-form-field style="width:100%;margin-bottom:4px">
          <mat-label>Coverage</mat-label>
          <mat-select formControlName="coverageRefId">

            <!-- Commercial: grouped by LOB -->
            <ng-container *ngIf="data.lobs.length > 0">
              <mat-optgroup *ngFor="let lob of data.lobs" [label]="lob.lobCode">
                <mat-option *ngFor="let c of lob.coverages" [value]="c.id">
                  <span style="font-weight:600">{{c.coverageCode}}</span>
                </mat-option>
              </mat-optgroup>
              <mat-optgroup *ngIf="data.coverages.length > 0" label="Unassigned">
                <mat-option *ngFor="let c of data.coverages" [value]="c.id">
                  <span style="font-weight:600">{{c.coverageCode}}</span>
                </mat-option>
              </mat-optgroup>
            </ng-container>

            <!-- Personal lines: flat -->
            <ng-container *ngIf="data.lobs.length === 0">
              <mat-option *ngFor="let c of data.coverages" [value]="c.id">
                <span style="font-weight:600">{{c.coverageCode}}</span>
              </mat-option>
            </ng-container>
          </mat-select>
        </mat-form-field>

        <!-- State selector -->
        <mat-form-field style="width:100%;margin-bottom:4px">
          <mat-label>State</mat-label>
          <mat-select formControlName="state">
            <!-- Declared states -->
            <mat-option *ngFor="let s of data.productStates" [value]="s.stateCode">
              {{s.stateCode}}
            </mat-option>
            <mat-divider *ngIf="data.productStates.length > 0"></mat-divider>
            <!-- Wildcard always available -->
            <mat-option value="*">* — All states (wildcard fallback)</mat-option>
            <!-- Custom entry -->
            <mat-option value="__custom__">Other (type below)…</mat-option>
          </mat-select>
        </mat-form-field>

        <!-- Custom state text field, shown when "Other" selected -->
        <mat-form-field *ngIf="form.value.state === '__custom__'" style="width:100%;margin-bottom:4px">
          <mat-label>State Code</mat-label>
          <input matInput formControlName="customState"
                 placeholder="e.g. TX" style="text-transform:uppercase">
          <mat-hint>2-letter state abbreviation</mat-hint>
        </mat-form-field>

        <mat-divider style="margin:16px 0"></mat-divider>

        <div style="display:flex;gap:12px">
          <!-- Version -->
          <mat-form-field style="flex:1">
            <mat-label>Version</mat-label>
            <input matInput formControlName="version" placeholder="e.g. 2026.01">
          </mat-form-field>

          <!-- Effective Start -->
          <mat-form-field style="flex:1">
            <mat-label>Effective Start</mat-label>
            <input matInput formControlName="effStart" placeholder="YYYY-MM-DD">
          </mat-form-field>
        </div>

      </form>

      <div *ngIf="error" style="color:#c62828;font-size:13px;margin-top:8px">
        {{error}}
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end" style="padding:12px 24px">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary"
              [disabled]="form.invalid || saving"
              (click)="save()">
        {{saving ? 'Creating…' : 'Create Mapping'}}
      </button>
    </mat-dialog-actions>
  `
})
export class AddMappingDialogComponent implements OnInit {
  readonly data = inject<AddMappingDialogData>(MAT_DIALOG_DATA);
  private dialogRef = inject(MatDialogRef<AddMappingDialogComponent>);
  private coverSvc  = inject(CoverageService);
  private fb        = inject(FormBuilder);

  form!: FormGroup;
  saving = false;
  error  = '';

  ngOnInit() {
    const today = new Date().toISOString().slice(0, 10);
    this.form = this.fb.group({
      coverageRefId: [this.data.preselectedCoverageRefId ?? null, Validators.required],
      state:         [this.data.productStates[0]?.stateCode ?? '*', Validators.required],
      customState:   [''],
      version:       ['', Validators.required],
      effStart:      [today, Validators.required],
    });
  }

  get resolvedState(): string {
    const s = this.form.value.state;
    if (s === '__custom__') return (this.form.value.customState ?? '').trim().toUpperCase();
    return s;
  }

  save() {
    if (this.form.invalid) return;
    const state = this.resolvedState;
    if (!state) { this.error = 'Please enter a state code.'; return; }

    this.saving = true;
    this.error  = '';

    this.coverSvc.create({
      coverageRefId: +this.form.value.coverageRefId,
      state,
      version:   this.form.value.version.trim(),
      effStart:  this.form.value.effStart,
      expireAt:  ACTIVE_EXPIRE,
      perils:    [],
      pipeline:  [],
    }).subscribe({
      next:  () => { this.saving = false; this.dialogRef.close(true); },
      error: (e) => {
        this.saving = false;
        this.error  = e?.error?.detail ?? e?.message ?? 'Failed to create mapping.';
      },
    });
  }
}
