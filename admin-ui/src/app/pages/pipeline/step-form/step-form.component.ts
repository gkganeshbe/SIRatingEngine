import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators
} from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  StepConfig, WhenUi, WhenOperator,
  whenUiToConfig, whenConfigToUi
} from '../../../core/models/api.models';

export interface StepFormData { step: StepConfig | null; }

@Component({
  selector: 'app-step-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatCheckboxModule,
    MatDividerModule, MatTooltipModule,
  ],
  template: `
    <h2 mat-dialog-title>{{isEdit ? 'Edit Step' : 'Add Pipeline Step'}}</h2>

    <mat-dialog-content style="max-height:75vh">
      <form [formGroup]="form" class="dialog-form">

        <!-- ── Identity ────────────────────────────────────────────────── -->
        <div class="form-row">
          <mat-form-field>
            <mat-label>Step ID</mat-label>
            <input matInput formControlName="id" placeholder="e.g. S1_GRP1_BASERATE">
            <mat-hint>Unique identifier within this pipeline</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Name (display)</mat-label>
            <input matInput formControlName="name" placeholder="e.g. Grp I Base Rate">
          </mat-form-field>
        </div>

        <mat-form-field class="full-width">
          <mat-label>Operation</mat-label>
          <mat-select formControlName="operation">
            <mat-option value="lookup">lookup – rate table factor</mat-option>
            <mat-option value="compute">compute – arithmetic expression</mat-option>
            <mat-option value="round">round – round running premium</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-divider style="margin:8px 0"></mat-divider>

        <!-- ── LOOKUP fields ────────────────────────────────────────────── -->
        <ng-container *ngIf="op === 'lookup'" formGroupName="lookupConfig">
          <mat-form-field class="full-width">
            <mat-label>Rate Table Name</mat-label>
            <input matInput formControlName="rateTable" placeholder="e.g. CondoBaseRate">
          </mat-form-field>

          <div class="form-row">
            <mat-form-field>
              <mat-label>Math Type</mat-label>
              <mat-select formControlName="mathType">
                <mat-option value="set">set (overwrite premium)</mat-option>
                <mat-option value="mul">mul (multiply)</mat-option>
                <mat-option value="add">add (add to premium)</mat-option>
                <mat-option value="sub">sub (subtract)</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Interpolate Key (optional)</mat-label>
              <input matInput formControlName="interpolateKey"
                     placeholder="e.g. InsuredValue"
                     matTooltip="Key column used for linear interpolation">
            </mat-form-field>
          </div>

          <mat-form-field class="full-width">
            <mat-label>Range Key (optional)</mat-label>
            <input matInput formControlName="rangeKey"
                   placeholder="e.g. InsuredValue"
                   matTooltip="Risk bag key whose value must fall within RangeFrom/RangeTo">
          </mat-form-field>

          <!-- Keys -->
          <div class="section-title">Lookup Keys</div>
          <div formArrayName="keys">
            <div *ngFor="let kv of keyControls; let i = index"
                 [formGroupName]="i" class="keys-row">
              <mat-form-field>
                <mat-label>Key Name</mat-label>
                <input matInput formControlName="keyName" placeholder="e.g. ConstructionType">
              </mat-form-field>
              <mat-form-field>
                <mat-label>Key Value / Path</mat-label>
                <input matInput formControlName="keyValue"
                       placeholder="e.g. &#36;risk.ConstructionType or *">
              </mat-form-field>
              <button mat-icon-button color="warn" type="button" (click)="removeKey(i)">
                <mat-icon>remove_circle</mat-icon>
              </button>
            </div>
          </div>
          <button mat-stroked-button type="button" (click)="addKey()" style="margin-bottom:8px">
            <mat-icon>add</mat-icon> Add Key
          </button>
        </ng-container>

        <!-- ── COMPUTE fields ───────────────────────────────────────────── -->
        <ng-container *ngIf="op === 'compute'" formGroupName="computeConfig">
          <mat-form-field class="full-width">
            <mat-label>Expression</mat-label>
            <input matInput formControlName="expr"
                   placeholder="e.g. &#36;premium * &#36;coverage.InsuredValue / 100">
            <mat-hint>Operands: &#36;risk.X, &#36;coverage.X, &#36;premium, or literals. Operators: + - * /</mat-hint>
          </mat-form-field>
          <div class="form-row" style="align-items:center">
            <mat-form-field>
              <mat-label>Store As (risk bag key)</mat-label>
              <input matInput formControlName="storeAs" placeholder="e.g. FinalRate">
            </mat-form-field>
            <mat-checkbox formControlName="applyToPremium" style="padding-top:8px">
              Apply result to running premium
            </mat-checkbox>
          </div>
        </ng-container>

        <!-- ── ROUND fields ─────────────────────────────────────────────── -->
        <ng-container *ngIf="op === 'round'" formGroupName="roundConfig">
          <div class="form-row">
            <mat-form-field>
              <mat-label>Decimal Precision</mat-label>
              <input matInput type="number" formControlName="precision" min="0" max="10">
            </mat-form-field>
            <mat-form-field>
              <mat-label>Rounding Mode</mat-label>
              <mat-select formControlName="mode">
                <mat-option value="AwayFromZero">AwayFromZero (standard)</mat-option>
                <mat-option value="ToEven">ToEven (banker's)</mat-option>
              </mat-select>
            </mat-form-field>
          </div>
        </ng-container>

        <mat-divider style="margin:8px 0"></mat-divider>

        <!-- ── When condition (all operations) ─────────────────────────── -->
        <div formGroupName="whenGroup">
          <mat-checkbox formControlName="hasWhen">Apply when condition</mat-checkbox>

          <ng-container *ngIf="form.get('whenGroup.hasWhen')?.value">
            <div class="form-row" style="margin-top:8px">
              <mat-form-field>
                <mat-label>Path</mat-label>
                <input matInput formControlName="path"
                       placeholder="e.g. &#36;peril or &#36;risk.HasPool">
                <mat-hint>&#36;peril, &#36;risk.X, or &#36;coverage.X</mat-hint>
              </mat-form-field>
              <mat-form-field>
                <mat-label>Operator</mat-label>
                <mat-select formControlName="operator">
                  <mat-option value="equals">equals</mat-option>
                  <mat-option value="notEquals">notEquals</mat-option>
                  <mat-option value="isTrue">isTrue</mat-option>
                  <mat-option value="greaterThan">greaterThan</mat-option>
                  <mat-option value="lessThan">lessThan</mat-option>
                  <mat-option value="greaterThanOrEqual">greaterThanOrEqual</mat-option>
                  <mat-option value="lessThanOrEqual">lessThanOrEqual</mat-option>
                  <mat-option value="in">in (comma-separated)</mat-option>
                  <mat-option value="notIn">notIn (comma-separated)</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field>
                <mat-label>Value</mat-label>
                <input matInput formControlName="value"
                       placeholder="e.g. GRP1 or GRP1,GRP2">
              </mat-form-field>
            </div>
          </ng-container>
        </div>

      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid">
        {{isEdit ? 'Update' : 'Add Step'}}
      </button>
    </mat-dialog-actions>
  `
})
export class StepFormComponent implements OnInit {
  form!: FormGroup;
  isEdit = false;

  readonly whenOperators: WhenOperator[] = [
    'equals','notEquals','isTrue','greaterThan','lessThan',
    'greaterThanOrEqual','lessThanOrEqual','in','notIn'
  ];

  private fb         = inject(FormBuilder);
  readonly dialogRef = inject(MatDialogRef<StepFormComponent>);
  readonly data      = inject<StepFormData>(MAT_DIALOG_DATA);

  ngOnInit() {
    this.isEdit = !!this.data.step;
    const s = this.data.step;

    // Derive existing when UI model
    const whenUi: WhenUi | null = s?.when ? whenConfigToUi(s.when) : null;

    this.form = this.fb.group({
      id:        [s?.id   ?? '', Validators.required],
      name:      [s?.name ?? ''],
      operation: [s?.operation ?? 'lookup', Validators.required],

      lookupConfig: this.fb.group({
        rateTable:     [s?.rateTable ?? '', s?.operation === 'lookup' ? Validators.required : []],
        mathType:      [s?.math?.type  ?? 'mul'],
        interpolateKey:[s?.interpolate?.key ?? ''],
        rangeKey:      [s?.rangeKey?.key    ?? ''],
        keys: this.fb.array(
          Object.entries(s?.keys ?? {}).map(([k, v]) => this.newKeyGroup(k, v))
        ),
      }),

      computeConfig: this.fb.group({
        expr:           [s?.compute?.expr    ?? ''],
        storeAs:        [s?.compute?.storeAs ?? ''],
        applyToPremium: [s?.compute?.applyToPremium ?? false],
      }),

      roundConfig: this.fb.group({
        precision: [s?.round?.precision ?? 2],
        mode:      [s?.round?.mode      ?? 'AwayFromZero'],
      }),

      whenGroup: this.fb.group({
        hasWhen:  [!!s?.when],
        path:     [whenUi?.path     ?? ''],
        operator: [whenUi?.operator ?? 'equals'],
        value:    [whenUi?.value    ?? ''],
      }),
    });
  }

  get op(): string { return this.form.get('operation')?.value ?? ''; }

  get keyControls(): AbstractControl[] {
    return (this.form.get('lookupConfig.keys') as FormArray).controls;
  }

  private newKeyGroup(name = '', value = ''): FormGroup {
    return this.fb.group({ keyName: [name, Validators.required], keyValue: [value, Validators.required] });
  }

  addKey() {
    (this.form.get('lookupConfig.keys') as FormArray).push(this.newKeyGroup());
  }

  removeKey(i: number) {
    (this.form.get('lookupConfig.keys') as FormArray).removeAt(i);
  }

  save() {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();

    const step: StepConfig = {
      id:        v.id.trim(),
      name:      v.name.trim(),
      operation: v.operation,
    };

    if (v.operation === 'lookup') {
      const lc = v.lookupConfig;
      step.rateTable = lc.rateTable;
      step.math      = { type: lc.mathType };
      if (lc.interpolateKey) step.interpolate = { key: lc.interpolateKey };
      if (lc.rangeKey)       step.rangeKey    = { key: lc.rangeKey };

      // Build keys record
      const keysArr: { keyName: string; keyValue: string }[] = lc.keys;
      if (keysArr.length > 0) {
        step.keys = Object.fromEntries(keysArr.map(k => [k.keyName, k.keyValue]));
      }
    }

    if (v.operation === 'compute') {
      const cc = v.computeConfig;
      step.compute = { expr: cc.expr, storeAs: cc.storeAs, applyToPremium: cc.applyToPremium };
    }

    if (v.operation === 'round') {
      const rc = v.roundConfig;
      step.round = { precision: +rc.precision, mode: rc.mode };
    }

    // When condition
    const wg = v.whenGroup;
    if (wg.hasWhen && wg.path) {
      step.when = whenUiToConfig({ path: wg.path, operator: wg.operator, value: wg.value });
    }

    this.dialogRef.close(step);
  }
}
