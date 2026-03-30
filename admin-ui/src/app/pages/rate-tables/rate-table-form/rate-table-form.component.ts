import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { RateTableService } from '../../../core/services/rate-table.service';
import { RateTableSummary, LookupType, ColumnDefRequest } from '../../../core/models/api.models';

export interface RateTableFormData {
  coverageId: number;
  table: RateTableSummary | null;
}

const COLUMN_NAMES = ['Key1','Key2','Key3','Key4','Key5','RangeFrom','RangeTo','Factor','Additive','AdditionalUnit','AdditionalRate'];
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
            <mat-label>Lookup Type</mat-label>
            <mat-select formControlName="lookupType">
              <mat-option value="EXACT">EXACT – exact key match</mat-option>
              <mat-option value="INTERPOLATE">INTERPOLATE – linear interpolation</mat-option>
              <mat-option value="RANGE">RANGE – RangeFrom/RangeTo dimension</mat-option>
              <mat-option value="WILDCARD">WILDCARD – single wildcard row</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field class="full-width">
          <mat-label>Description (optional)</mat-label>
          <input matInput formControlName="description">
        </mat-form-field>

        <div class="form-row">
          <mat-form-field>
            <mat-label>Eff Start (YYYY-MM-DD)</mat-label>
            <input matInput formControlName="effStart">
          </mat-form-field>
          <mat-form-field *ngIf="isEdit">
            <mat-label>Expire At (optional)</mat-label>
            <input matInput formControlName="expireAt">
          </mat-form-field>
          <mat-form-field *ngIf="form.value.lookupType === 'INTERPOLATE'">
            <mat-label>Interpolation Key Column</mat-label>
            <mat-select formControlName="interpolationKeyCol">
              <mat-option value="Key1">Key1</mat-option>
              <mat-option value="Key2">Key2</mat-option>
              <mat-option value="Key3">Key3</mat-option>
              <mat-option value="Key4">Key4</mat-option>
              <mat-option value="Key5">Key5</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <!-- Column Defs (create only) -->
        <ng-container *ngIf="!isEdit">
          <div class="section-title" style="margin-top:8px">
            Column Definitions
            <span style="font-weight:300;font-size:11px;margin-left:8px">
              (define at least Factor or Additive)
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
      description:         [t?.description         ?? ''],
      effStart:            [{ value: t?.effStart ?? '', disabled: this.isEdit }, this.isEdit ? [] : Validators.required],
      expireAt:            [t?.expireAt            ?? ''],
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
        description:         v.description  || null,
        lookupType:          v.lookupType   as LookupType,
        interpolationKeyCol: v.interpolationKeyCol || null,
        expireAt:            v.expireAt     || null,
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
        description:         v.description  || null,
        lookupType:          v.lookupType   as LookupType,
        interpolationKeyCol: v.interpolationKeyCol || null,
        effStart:            v.effStart,
        expireAt:            v.expireAt     || null,
        columnDefs:          colDefs,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    }
  }
}
