import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ACTIVE_EXPIRE } from '../../../core/utils/date.utils';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { RateTableService } from '../../../core/services/rate-table.service';
import { RateTableSummary, LookupType, ValueType, ColumnDefRequest } from '../../../core/models/api.models';

export interface RateTableFormData {
  coverageId: number;
  table: RateTableSummary | null;
}

const COLUMN_NAMES = ['Key1','Key2','Key3','Key4','Key5','RangeFrom','RangeTo','Factor','AdditionalUnit','AdditionalRate'];
const DATA_TYPES   = ['string','numeric','date'];

@Component({
  selector: 'app-rate-table-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatCheckboxModule,
  ],
  template: `
    <h2 mat-dialog-title>{{isEdit ? 'Edit Rate Table' : 'New Rate Table'}}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <div class="form-row">
          <mat-form-field>
            <mat-label>Table Name</mat-label>
            <input matInput formControlName="name" placeholder="e.g. CondoBaseRate">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Matching Method</mat-label>
            <mat-select formControlName="lookupType">
              <mat-option value="EXACT">Exact Match</mat-option>
              <mat-option value="INTERPOLATE">Linear Interpolation</mat-option>
              <mat-option value="RANGE">Numeric Range (From / To)</mat-option>
              <mat-option value="WILDCARD">Wildcard Fallback</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <div class="form-row">
          <mat-form-field>
            <mat-label>Value Type</mat-label>
            <mat-select formControlName="valueType">
              <mat-option value="Factor">Factor — multiply running premium</mat-option>
              <mat-option value="Rate">Rate — set as base (e.g. per $100 TIV)</mat-option>
              <mat-option value="FlatAmount">Flat Amount — add or subtract</mat-option>
              <mat-option value="Multiplier">Multiplier — same as Factor, semantic label</mat-option>
            </mat-select>
            <mat-hint>Informs the default math operation when this table is used in a step</mat-hint>
          </mat-form-field>
        </div>

        <mat-form-field class="full-width">
          <mat-label>Description (optional)</mat-label>
          <input matInput formControlName="description">
        </mat-form-field>

        <mat-form-field class="full-width">
          <mat-label>Intended for Coverage (optional)</mat-label>
          <input matInput formControlName="intendedCoverage"
                 placeholder="e.g. Building Coverage, Earthquake">
          <mat-hint>Documents which coverage(s) this table is designed for</mat-hint>
        </mat-form-field>

        <div class="form-row">
          <mat-form-field>
            <mat-label>Effective From (YYYY-MM-DD)</mat-label>
            <input matInput formControlName="effStart">
          </mat-form-field>
          <mat-form-field *ngIf="isEdit">
            <mat-label>Effective To (optional)</mat-label>
            <input matInput formControlName="expireAt">
          </mat-form-field>
          <mat-form-field *ngIf="form.value.lookupType === 'INTERPOLATE'">
            <mat-label>Interpolation Column</mat-label>
            <mat-select formControlName="interpolationKeyCol">
              <mat-option *ngFor="let col of colDefs.controls; let i = index"
                          [value]="getColName(i)">
                {{getColLabel(i)}}
              </mat-option>
            </mat-select>
            <mat-hint>The numeric key column used for interpolation between breakpoints</mat-hint>
          </mat-form-field>
        </div>

        <!-- Column Defs (create only) -->
        <ng-container *ngIf="!isEdit">
          <div class="section-title" style="margin-top:8px">
            Key & Value Columns
            <span style="font-weight:300;font-size:11px;margin-left:8px">
              (at least one value column required)
            </span>
          </div>
          <div formArrayName="columnDefs">
            <div *ngFor="let col of colDefs.controls; let i = index"
                 [formGroupName]="i" class="keys-row" style="flex-wrap:wrap;gap:6px">
              <mat-form-field style="flex:0 0 120px">
                <mat-label>Column</mat-label>
                <mat-select formControlName="columnName">
                  <mat-option *ngFor="let cn of columnNames" [value]="cn">{{cn}}</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field style="flex:1;min-width:120px">
                <mat-label>Display Label</mat-label>
                <input matInput formControlName="displayLabel" placeholder="e.g. Construction Type">
              </mat-form-field>
              <mat-form-field style="flex:0 0 100px">
                <mat-label>Data Type</mat-label>
                <mat-select formControlName="dataType">
                  <mat-option *ngFor="let dt of dataTypes" [value]="dt">{{dt}}</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-checkbox formControlName="isRequired" style="padding-top:12px">Req</mat-checkbox>
              <button mat-icon-button color="warn" type="button" (click)="removeColDef(i)">
                <mat-icon>remove_circle</mat-icon>
              </button>
            </div>
          </div>
          <button mat-stroked-button type="button" (click)="addColDef()" style="margin-bottom:8px">
            <mat-icon>add</mat-icon> Add Column
          </button>
        </ng-container>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        {{saving ? 'Saving…' : 'Save'}}
      </button>
    </mat-dialog-actions>
  `
})
export class RateTableFormComponent implements OnInit {
  form!: FormGroup;
  isEdit = false;
  saving = false;
  columnNames = COLUMN_NAMES;
  dataTypes   = DATA_TYPES;

  getColName(i: number): string {
    return this.colDefs.at(i)?.get('columnName')?.value ?? '';
  }
  getColLabel(i: number): string {
    const label = this.colDefs.at(i)?.get('displayLabel')?.value ?? '';
    const name  = this.getColName(i);
    return label || name;
  }

  private fb         = inject(FormBuilder);
  private svc        = inject(RateTableService);
  readonly dialogRef = inject(MatDialogRef<RateTableFormComponent>);
  readonly data      = inject<RateTableFormData>(MAT_DIALOG_DATA);

  ngOnInit() {
    this.isEdit = !!this.data.table;
    const t = this.data.table;

    this.form = this.fb.group({
      name:                [{ value: t?.name ?? '', disabled: this.isEdit }, Validators.required],
      lookupType:          [t?.lookupType          ?? 'EXACT', Validators.required],
      valueType:           [t?.valueType           ?? 'Factor', Validators.required],
      description:         [t?.description         ?? ''],
      intendedCoverage:    [t?.intendedCoverage     ?? ''],
      effStart:            [{ value: t?.effStart ?? '', disabled: this.isEdit }, this.isEdit ? [] : Validators.required],
      expireAt:            [t?.expireAt            ?? ACTIVE_EXPIRE],
      interpolationKeyCol: [t?.interpolationKeyCol ?? ''],
      columnDefs: this.fb.array([]),
    });

    if (!this.isEdit) {
      // Add default Factor column to get started
      this.addColDef('Factor', 'Factor', 'numeric', false);
    }
  }

  get colDefs(): FormArray { return this.form.get('columnDefs') as FormArray; }

  addColDef(columnName = '', displayLabel = '', dataType = 'string', isRequired = false) {
    this.colDefs.push(this.fb.group({
      columnName:   [columnName,   Validators.required],
      displayLabel: [displayLabel, Validators.required],
      dataType:     [dataType,     Validators.required],
      sortOrder:    [this.colDefs.length],
      isRequired:   [isRequired],
    }));
  }

  removeColDef(i: number) { this.colDefs.removeAt(i); }

  save() {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.getRawValue();
    const cid = this.data.coverageId;

    if (this.isEdit) {
      this.svc.update(cid, this.data.table!.id, {
        description:         v.description        || null,
        intendedCoverage:    v.intendedCoverage    || null,
        lookupType:          v.lookupType          as LookupType,
        valueType:           v.valueType           as ValueType,
        interpolationKeyCol: v.interpolationKeyCol || null,
        expireAt:            v.expireAt            || ACTIVE_EXPIRE,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    } else {
      const colDefs: ColumnDefRequest[] = (v.columnDefs as ColumnDefRequest[]).map((c, i) => ({
        ...c, sortOrder: i
      }));
      this.svc.create(cid, {
        coverageConfigId:    cid,
        name:                v.name,
        description:         v.description        || null,
        intendedCoverage:    v.intendedCoverage    || null,
        lookupType:          v.lookupType          as LookupType,
        valueType:           v.valueType           as ValueType,
        interpolationKeyCol: v.interpolationKeyCol || null,
        effStart:            v.effStart,
        expireAt:            v.expireAt            || ACTIVE_EXPIRE,
        columnDefs:          colDefs,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    }
  }
}
