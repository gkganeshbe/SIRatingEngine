import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ACTIVE_EXPIRE } from '../../../core/utils/date.utils';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CoverageService } from '../../../core/services/coverage.service';
import { AggFunction, AggregateConfigDetail, CoverageSummary } from '../../../core/models/api.models';

/** Dialog data for new config: pass coverageRefId + coverageCode (for display). */
export interface NewConfigData { coverageRefId: number; coverageCode: string; state?: string; }

const AGG_FUNCTIONS: { value: AggFunction; label: string }[] = [
  { value: 'SUM',   label: 'SUM – add up all values' },
  { value: 'AVG',   label: 'AVG – arithmetic mean' },
  { value: 'MAX',   label: 'MAX – highest value' },
  { value: 'MIN',   label: 'MIN – lowest value' },
  { value: 'COUNT', label: 'COUNT – number of risks' },
];

const AGG_OPERATORS = [
  { value: 'eq',  label: 'equals (eq)' },
  { value: 'ne',  label: 'not equals (ne)' },
  { value: 'in',  label: 'in list (in)' },
];

@Component({
  selector: 'app-coverage-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule,
    MatDividerModule, MatCheckboxModule, MatSelectModule, MatTooltipModule,
  ],
  template: `
    <h2 mat-dialog-title>{{isEdit ? 'Edit State Config' : 'New State Config'}}
      <span *ngIf="coverageCode" style="font-weight:400;font-size:14px;color:rgba(0,0,0,.54)">
        &mdash; {{coverageCode}}
      </span>
    </h2>
    <mat-dialog-content style="max-height:82vh">
      <form [formGroup]="form" class="dialog-form">
        <div class="form-row">
          <mat-form-field>
            <mat-label>State</mat-label>
            <input matInput formControlName="state" placeholder="e.g. NJ or * for all states">
            <mat-hint>Use * as a wildcard fallback for all unspecified states</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Version</mat-label>
            <input matInput formControlName="version" placeholder="e.g. 2026.02">
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field>
            <mat-label>Effective From (YYYY-MM-DD)</mat-label>
            <input matInput formControlName="effStart">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Effective To (optional)</mat-label>
            <input matInput formControlName="expireAt">
          </mat-form-field>
        </div>

        <div class="section-title" style="margin-top:8px">Perils (one per line)</div>
        <mat-form-field class="full-width">
          <mat-label>Perils</mat-label>
          <textarea matInput formControlName="perilsText" rows="3"
                    placeholder="GRP1&#10;GRP2&#10;SPL"></textarea>
          <mat-hint>One peril code per line. The pipeline runs once per peril.</mat-hint>
        </mat-form-field>

        <mat-divider style="margin:12px 0"></mat-divider>

        <div class="section-title" style="margin-top:4px">Cross-Coverage Dependencies</div>
        <mat-form-field class="full-width">
          <mat-label>Requires These Coverages to Be Rated First (one code per line)</mat-label>
          <textarea matInput formControlName="dependsOnText" rows="2"
                    placeholder="COVA&#10;COVB"></textarea>
          <mat-hint>
            These coverages will be rated before this one so their premiums are available here.
          </mat-hint>
        </mat-form-field>

        <mat-form-field class="full-width" style="margin-top:4px">
          <mat-label>Share These Results with Other Coverages (one key per line)</mat-label>
          <textarea matInput formControlName="publishText" rows="2"
                    placeholder="BaseRate&#10;FinalRate"></textarea>
          <mat-hint>
            After rating, these named results will be available to other coverages in this product.
          </mat-hint>
        </mat-form-field>

        <mat-divider style="margin:12px 0"></mat-divider>

        <!-- ── Aggregate Rating Mode ────────────────────────────────────────── -->
        <div style="display:flex;align-items:center;gap:10px;margin-bottom:8px">
          <div class="section-title" style="margin:0">Aggregate Rating Mode</div>
          <mat-checkbox formControlName="hasAggregate">Enable</mat-checkbox>
        </div>

        <p style="font-size:12px;color:rgba(0,0,0,.54);margin:0 0 10px">
          When enabled, the engine switches from rating each building/risk individually to
          aggregating fields across all standard risks and running the pipeline <strong>once</strong>
          at the LOB level. SCHEDLEVEL risks are always rated individually.
        </p>

        <ng-container *ngIf="form.get('hasAggregate')?.value" formGroupName="aggregateGroup">
          <!-- Trigger condition -->
          <div style="font-size:13px;font-weight:600;margin-bottom:6px">
            Activate When
            <span style="font-size:11px;font-weight:400;color:rgba(0,0,0,.54);margin-left:6px">
              (checked against the LOB-level merged risk bag at rating time)
            </span>
          </div>
          <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:flex-start">
            <mat-form-field style="flex:1;min-width:180px">
              <mat-label>Policy Field</mat-label>
              <input matInput formControlName="whenPath" placeholder="e.g. $risk.ValuationMethod">
            </mat-form-field>
            <mat-form-field style="flex:0 0 150px">
              <mat-label>Operator</mat-label>
              <mat-select formControlName="whenOp">
                <mat-option *ngFor="let o of aggOperators" [value]="o.value">{{o.label}}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field style="flex:0 0 140px">
              <mat-label>Value</mat-label>
              <input matInput formControlName="whenValue" placeholder="e.g. RC">
            </mat-form-field>
          </div>

          <!-- Aggregate fields -->
          <div style="display:flex;align-items:center;gap:8px;margin:10px 0 6px">
            <span style="font-size:13px;font-weight:600">Aggregate Fields</span>
            <button mat-stroked-button type="button" style="height:28px;font-size:11px"
                    (click)="addAggField()">
              <mat-icon style="font-size:15px;width:15px;height:15px">add</mat-icon> Add Field
            </button>
          </div>
          <p style="font-size:12px;color:rgba(0,0,0,.54);margin:0 0 8px">
            Each field is aggregated across all standard risks and injected as
            <code>$risk.&#123;ResultKey&#125;</code> into the aggregate pipeline context.
          </p>

          <div formArrayName="aggFields">
            <div *ngFor="let fg of aggFieldControls; let i = index"
                 [formGroupName]="i"
                 style="display:flex;gap:8px;align-items:flex-start;margin-bottom:6px;
                        background:#f9f9f9;padding:8px;border-radius:4px">
              <mat-form-field style="flex:1;min-width:130px">
                <mat-label>Source Field</mat-label>
                <input matInput formControlName="sourceField" placeholder="e.g. BuildingValue">
                <mat-hint>Policy field name, or * for COUNT</mat-hint>
              </mat-form-field>
              <mat-form-field style="flex:0 0 170px">
                <mat-label>Aggregation Function</mat-label>
                <mat-select formControlName="aggFunction">
                  <mat-option *ngFor="let f of aggFunctions" [value]="f.value">{{f.label}}</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field style="flex:1;min-width:140px">
                <mat-label>Save Aggregated Result As</mat-label>
                <input matInput formControlName="resultKey" placeholder="e.g. TotalBuildingValue">
                <mat-hint>Available as a field in subsequent rating steps</mat-hint>
              </mat-form-field>
              <button mat-icon-button color="warn" type="button"
                      (click)="removeAggField(i)" matTooltip="Remove field"
                      style="margin-top:6px;flex-shrink:0">
                <mat-icon>remove_circle</mat-icon>
              </button>
            </div>
          </div>

          <div *ngIf="aggFieldControls.length === 0"
               style="padding:12px;text-align:center;color:#bbb;font-size:12px;
                      background:#fafafa;border:1px dashed #ddd;border-radius:4px">
            No fields defined — the aggregate context will only contain the LOB base risk attributes.
          </div>
        </ng-container>

        <!-- ── Documentation ───────────────────────────────────────────── -->
        <mat-divider style="margin:12px 0"></mat-divider>
        <details>
          <summary style="cursor:pointer;font-size:13px;color:rgba(0,0,0,.6);user-select:none">
            Documentation / Notes (optional)
          </summary>
          <mat-form-field style="width:100%;margin-top:8px">
            <mat-label>Notes</mat-label>
            <textarea matInput formControlName="notes" rows="3"
                      placeholder="Describe this state config, filing reference, or any exceptions to note…"></textarea>
          </mat-form-field>
        </details>

      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        {{saving ? 'Saving\u2026' : 'Save'}}
      </button>
    </mat-dialog-actions>
  `
})
export class CoverageFormComponent implements OnInit {
  form!: FormGroup;
  isEdit = false;
  saving = false;
  coverageCode = '';

  readonly aggFunctions = AGG_FUNCTIONS;
  readonly aggOperators = AGG_OPERATORS;

  private fb         = inject(FormBuilder);
  private svc        = inject(CoverageService);
  readonly dialogRef = inject(MatDialogRef<CoverageFormComponent>);
  readonly data      = inject<CoverageSummary | NewConfigData>(MAT_DIALOG_DATA);

  private get existing(): CoverageSummary | null {
    return this.data && 'id' in this.data ? this.data as CoverageSummary : null;
  }

  private get newData(): NewConfigData | null {
    return this.data && 'coverageRefId' in this.data ? this.data as NewConfigData : null;
  }

  get aggFieldControls() {
    return (this.form.get('aggregateGroup.aggFields') as FormArray).controls;
  }

  ngOnInit() {
    this.isEdit       = !!this.existing;
    this.coverageCode = this.existing?.coverageCode ?? this.newData?.coverageCode ?? '';

    const ex = this.existing;
    this.form = this.fb.group({
      state:         [{ value: ex?.state   ?? this.newData?.state ?? '*', disabled: this.isEdit }, Validators.required],
      version:       [{ value: ex?.version ?? '',  disabled: this.isEdit }, Validators.required],
      effStart:      [ex?.effStart ?? '', Validators.required],
      expireAt:      [ex?.expireAt ?? ACTIVE_EXPIRE],
      perilsText:    [''],
      dependsOnText: [''],
      publishText:   [''],
      notes:         [''],
      hasAggregate:  [false],
      aggregateGroup: this.fb.group({
        whenPath:  [''],
        whenOp:    ['eq'],
        whenValue: [''],
        aggFields: this.fb.array([]),
      }),
    });

    if (this.isEdit && ex) {
      this.svc.get(ex.id).subscribe(d => {
        this.form.patchValue({
          perilsText:    d.perils.join('\n'),
          dependsOnText: (d.dependsOn ?? []).join('\n'),
          publishText:   (d.publish   ?? []).join('\n'),
          notes:         d.notes ?? '',
          hasAggregate:  !!d.aggregate,
        });
        if (d.aggregate) {
          this.patchAggregateConfig(d.aggregate);
        }
      });
    }
  }

  private patchAggregateConfig(agg: AggregateConfigDetail) {
    this.form.get('aggregateGroup')!.patchValue({
      whenPath:  agg.whenPath,
      whenOp:    agg.whenOp,
      whenValue: agg.whenValue,
    });
    const arr = this.form.get('aggregateGroup.aggFields') as FormArray;
    arr.clear();
    for (const f of agg.fields) {
      arr.push(this.fb.group({
        sourceField: [f.sourceField, Validators.required],
        aggFunction: [f.aggFunction, Validators.required],
        resultKey:   [f.resultKey,   Validators.required],
      }));
    }
  }

  addAggField() {
    (this.form.get('aggregateGroup.aggFields') as FormArray).push(
      this.fb.group({
        sourceField: ['', Validators.required],
        aggFunction: ['SUM', Validators.required],
        resultKey:   ['', Validators.required],
      })
    );
  }

  removeAggField(i: number) {
    (this.form.get('aggregateGroup.aggFields') as FormArray).removeAt(i);
  }

  save() {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.getRawValue();

    const splitLines = (text: string): string[] =>
      text.split('\n').map(s => s.trim()).filter(s => s.length > 0);

    const perils    = splitLines(v.perilsText);
    const dependsOn = splitLines(v.dependsOnText);
    const publish   = splitLines(v.publishText);

    const aggregate = v.hasAggregate && v.aggregateGroup.whenPath
      ? {
          whenPath:  v.aggregateGroup.whenPath,
          whenOp:    v.aggregateGroup.whenOp,
          whenValue: v.aggregateGroup.whenValue,
          fields: (v.aggregateGroup.aggFields as any[]).map((f, i) => ({
            sourceField: f.sourceField,
            aggFunction: f.aggFunction,
            resultKey:   f.resultKey,
            sortOrder:   i,
          })),
        }
      : null;

    const notes = v.notes?.trim() || null;

    if (this.isEdit && this.existing) {
      this.svc.update(this.existing.id, {
        effStart: v.effStart, expireAt: v.expireAt || ACTIVE_EXPIRE,
        notes, perils, pipeline: [], dependsOn, publish, aggregate,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    } else {
      this.svc.create({
        coverageRefId: this.newData!.coverageRefId,
        state: v.state, version: v.version,
        effStart: v.effStart, expireAt: v.expireAt || ACTIVE_EXPIRE,
        notes, perils, pipeline: [], dependsOn, publish, aggregate,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    }
  }
}
