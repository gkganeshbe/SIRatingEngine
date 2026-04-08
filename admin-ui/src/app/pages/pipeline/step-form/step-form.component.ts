import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl, FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators
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
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import {
  StepConfig, WhenOperator, RiskField,
  WhenGroup, whenConfigToGroups, groupsToWhenConfig
} from '../../../core/models/api.models';
import { RiskFieldService } from '../../../core/services/risk-field.service';

export interface StepFormData {
  step: StepConfig | null;
  /** ProductCode of the coverage being edited — used to load product-specific risk fields. */
  productCode?: string;
}

@Component({
  selector: 'app-step-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatCheckboxModule,
    MatDividerModule, MatTooltipModule, MatAutocompleteModule,
  ],
  template: `
    <h2 mat-dialog-title>{{isEdit ? 'Edit Step' : 'Add Pipeline Step'}}</h2>

    <mat-dialog-content style="max-height:75vh">
      <form [formGroup]="form" class="dialog-form">

        <!-- ── Identity ────────────────────────────────────────────────── -->
        <div class="form-row">
          <mat-form-field>
            <mat-label>Step Name</mat-label>
            <input matInput formControlName="name" placeholder="e.g. Territory Factor">
            <mat-hint>Friendly label shown in the pipeline flow</mat-hint>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Step Code</mat-label>
            <input matInput formControlName="id" placeholder="e.g. S1_GRP1_BASERATE">
            <mat-hint>Unique identifier within this pipeline</mat-hint>
          </mat-form-field>
        </div>

        <mat-form-field class="full-width">
          <mat-label>Step Type</mat-label>
          <mat-select formControlName="operation">
            <mat-option value="lookup">Lookup — read a factor or rate from a rate table</mat-option>
            <mat-option value="compute">Compute — establish a value from an expression</mat-option>
            <mat-option value="adjustment">Adjustment — modify the running premium</mat-option>
            <mat-option value="round">Rounding — round the running premium</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-divider style="margin:8px 0"></mat-divider>

        <!-- ── LOOKUP fields ────────────────────────────────────────────── -->
        <ng-container *ngIf="op === 'lookup'" formGroupName="lookupConfig">
          <mat-form-field class="full-width">
            <mat-label>Rate Table Name</mat-label>
            <input matInput formControlName="rateTable" placeholder="e.g. CondoBaseRate">
          </mat-form-field>

          <mat-form-field class="full-width">
            <mat-label>Calculation Type</mat-label>
            <mat-select formControlName="mathType">
              <mat-option value="set">Set as base value — result becomes the new running premium</mat-option>
              <mat-option value="mul">Multiply by — running premium × factor</mat-option>
              <mat-option value="add">Add flat amount — running premium + amount</mat-option>
              <mat-option value="sub">Subtract flat amount — running premium − amount</mat-option>
            </mat-select>
          </mat-form-field>

          <!-- Keys — defined first so Interpolation Column can select from them -->
          <div class="section-title">Lookup Keys</div>
          <div formArrayName="keys">
            <div *ngFor="let kv of keyControls; let i = index"
                 [formGroupName]="i" class="keys-row">
              <mat-form-field>
                <mat-label>Lookup Column</mat-label>
                <input matInput formControlName="keyName" placeholder="e.g. ConstructionType">
              </mat-form-field>
              <mat-form-field>
                <mat-label>Policy Field</mat-label>
                <input matInput formControlName="keyValue"
                       placeholder="Type or select a risk field…"
                       [matAutocomplete]="keyValueAuto">
                <mat-hint *ngIf="riskFields.length > 0">Start typing a field name or path</mat-hint>
                <mat-autocomplete #keyValueAuto>
                  <mat-optgroup *ngFor="let grp of groupedFields"
                                [label]="grp.category">
                    <mat-option *ngFor="let f of grp.fields" [value]="f.path">
                      <span>{{f.displayName}}</span>
                      <small style="color:#999;margin-left:8px">{{f.path}}</small>
                    </mat-option>
                  </mat-optgroup>
                  <mat-optgroup *ngIf="riskFields.length === 0" label="No fields configured">
                    <mat-option disabled>Go to Risk Fields to add entries</mat-option>
                  </mat-optgroup>
                </mat-autocomplete>
              </mat-form-field>
              <button mat-icon-button color="warn" type="button" (click)="removeKey(i)">
                <mat-icon>remove_circle</mat-icon>
              </button>
            </div>
          </div>
          <button mat-stroked-button type="button" (click)="addKey()" style="margin-bottom:8px">
            <mat-icon>add</mat-icon> Add Key
          </button>

          <mat-divider style="margin:8px 0"></mat-divider>

          <!-- Interpolation Column — dropdown restricted to key names defined above -->
          <div class="form-row">
            <mat-form-field>
              <mat-label>Interpolation Column (optional)</mat-label>
              <mat-select formControlName="interpolateKey"
                          matTooltip="Designates one lookup column as the numeric interpolation dimension. Must be one of the lookup columns defined above.">
                <mat-option value="">— none —</mat-option>
                <mat-option *ngFor="let k of keyNames" [value]="k">{{k}}</mat-option>
              </mat-select>
              <mat-hint *ngIf="keyNames.length === 0">Add lookup keys above to enable interpolation</mat-hint>
              <mat-hint *ngIf="keyNames.length > 0">The value at this column must be numeric; the engine interpolates between rate table breakpoints</mat-hint>
              <mat-error *ngIf="form.get('lookupConfig.interpolateKey')?.hasError('notAKeyName')">
                Must match one of the lookup columns defined above
              </mat-error>
            </mat-form-field>

            <!--
              Range Lookup Field — reads a value from the risk bag and compares it
              against RangeFrom/RangeTo in the rate table rows.
              This is a risk bag field reference, NOT one of the step's lookup key names.
            -->
            <mat-form-field>
              <mat-label>Range Lookup Field (optional)</mat-label>
              <input matInput formControlName="rangeKey"
                     placeholder="Type or select a risk field…"
                     [matAutocomplete]="rangeKeyAuto"
                     matTooltip="The policy field whose numeric value is compared against each rate table row's RangeFrom/RangeTo bounds. Select from the registry or type a path directly.">
              <mat-hint>Policy field used for range matching — not a lookup column</mat-hint>
              <mat-autocomplete #rangeKeyAuto>
                <mat-optgroup *ngFor="let grp of groupedFields" [label]="grp.category">
                  <mat-option *ngFor="let f of grp.fields" [value]="f.path">
                    <span>{{f.displayName}}</span>
                    <small style="color:#999;margin-left:8px">{{f.path}}</small>
                  </mat-option>
                </mat-optgroup>
              </mat-autocomplete>
            </mat-form-field>
          </div>
        </ng-container>

        <!-- ── ADJUSTMENT fields ───────────────────────────────────────── -->
        <ng-container *ngIf="op === 'adjustment'" formGroupName="adjustmentConfig">
          <mat-form-field class="full-width">
            <mat-label>Calculation Type</mat-label>
            <mat-select formControlName="mathType">
              <mat-option value="set">Set as base value — result becomes the new running premium</mat-option>
              <mat-option value="mul">Multiply by — running premium × factor</mat-option>
              <mat-option value="add">Add flat amount — running premium + amount</mat-option>
              <mat-option value="sub">Subtract flat amount — running premium − amount</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field class="full-width">
            <mat-label>Source</mat-label>
            <mat-select formControlName="sourceType">
              <mat-option value="rateTable">Rate Table — read the value from a configured rate table</mat-option>
              <mat-option value="constant">Constant Value — apply a fixed number</mat-option>
              <mat-option value="stepOutput">Prior Step Output — read a named result from an earlier step</mat-option>
            </mat-select>
          </mat-form-field>

          <!-- Rate table source -->
          <ng-container *ngIf="adjSource === 'rateTable'">
            <mat-form-field class="full-width">
              <mat-label>Rate Table Name</mat-label>
              <input matInput formControlName="rateTable" placeholder="e.g. TerritoryFactor">
            </mat-form-field>
            <div class="section-title">Lookup Keys</div>
            <div formArrayName="keys">
              <div *ngFor="let kv of adjKeyControls; let i = index"
                   [formGroupName]="i" class="keys-row">
                <mat-form-field>
                  <mat-label>Lookup Column</mat-label>
                  <input matInput formControlName="keyName" placeholder="e.g. Territory">
                </mat-form-field>
                <mat-form-field>
                  <mat-label>Policy Field</mat-label>
                  <input matInput formControlName="keyValue"
                         placeholder="Type or select a risk field…"
                         [matAutocomplete]="adjKeyAuto">
                  <mat-autocomplete #adjKeyAuto>
                    <mat-optgroup *ngFor="let grp of groupedFields" [label]="grp.category">
                      <mat-option *ngFor="let f of grp.fields" [value]="f.path">
                        <span>{{f.displayName}}</span>
                        <small style="color:#999;margin-left:8px">{{f.path}}</small>
                      </mat-option>
                    </mat-optgroup>
                  </mat-autocomplete>
                </mat-form-field>
                <button mat-icon-button color="warn" type="button" (click)="removeAdjKey(i)">
                  <mat-icon>remove_circle</mat-icon>
                </button>
              </div>
            </div>
            <button mat-stroked-button type="button" (click)="addAdjKey()" style="margin-bottom:8px">
              <mat-icon>add</mat-icon> Add Key
            </button>
          </ng-container>

          <!-- Constant source -->
          <mat-form-field *ngIf="adjSource === 'constant'" class="full-width">
            <mat-label>Value</mat-label>
            <input matInput type="number" formControlName="constantValue" placeholder="e.g. 1.15">
            <mat-hint>The fixed value applied by the calculation type above</mat-hint>
          </mat-form-field>

          <!-- Step output source -->
          <mat-form-field *ngIf="adjSource === 'stepOutput'" class="full-width">
            <mat-label>Prior Step Result</mat-label>
            <input matInput formControlName="stepOutputRef"
                   placeholder="e.g. $risk.TerritoryFactor"
                   [matAutocomplete]="stepOutputAuto">
            <mat-hint>Risk-bag key written by an earlier step's "Save Result As" field</mat-hint>
            <mat-autocomplete #stepOutputAuto>
              <mat-optgroup *ngFor="let grp of groupedFields" [label]="grp.category">
                <mat-option *ngFor="let f of grp.fields" [value]="f.path">
                  <span>{{f.displayName}}</span>
                  <small style="color:#999;margin-left:8px">{{f.path}}</small>
                </mat-option>
              </mat-optgroup>
            </mat-autocomplete>
          </mat-form-field>
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
              <mat-label>Save Result As</mat-label>
              <input matInput formControlName="storeAs" placeholder="e.g. FinalRate">
              <mat-hint>Named result available to subsequent steps and dependent coverages</mat-hint>
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

        <!-- ── Advanced Options ────────────────────────────────────────── -->
        <mat-divider style="margin:8px 0"></mat-divider>
        <div class="form-row">
          <mat-form-field style="flex:2">
            <mat-label>Save Result As (optional)</mat-label>
            <input matInput formControlName="outputAlias" placeholder="e.g. TerritoryFactor">
            <mat-hint>Makes this step's result available as $risk.&#123;name&#125; in downstream steps</mat-hint>
          </mat-form-field>
          <mat-form-field style="flex:1">
            <mat-label>Operation Scope (optional)</mat-label>
            <mat-select formControlName="operationScope">
              <mat-option value="">— engine default —</mat-option>
              <mat-option value="policy">Policy — once per policy</mat-option>
              <mat-option value="coverage">Coverage — once per coverage</mat-option>
              <mat-option value="peril">Peril — once per peril</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <mat-divider style="margin:8px 0"></mat-divider>

        <!-- ── When condition (DNF group model) ────────────────────────── -->
        <div>
          <div style="display:flex;align-items:center;gap:10px;margin-bottom:4px">
            <span style="font-size:13px;font-weight:600">When Condition</span>
            <mat-checkbox [checked]="hasWhen" (change)="toggleWhen($event.checked)">
              Apply when condition
            </mat-checkbox>
          </div>

          <ng-container *ngIf="hasWhen">
            <p style="font-size:11px;color:rgba(0,0,0,.54);margin:0 0 8px">
              Clauses within a group are <strong>AND</strong>ed.
              Multiple groups are <strong>OR</strong>ed (DNF).
            </p>

            <div *ngFor="let group of whenGroups; let gi = index">
              <!-- OR separator between groups -->
              <div *ngIf="gi > 0"
                   style="display:flex;align-items:center;gap:8px;margin:6px 0">
                <div style="flex:1;height:1px;background:#e0e0e0"></div>
                <span style="font-size:11px;font-weight:700;color:#e65100;
                             background:#fff3e0;border:1px solid #ffcc80;
                             border-radius:10px;padding:1px 10px">OR</span>
                <div style="flex:1;height:1px;background:#e0e0e0"></div>
                <button mat-icon-button color="warn" style="width:24px;height:24px"
                        (click)="removeGroup(gi)" matTooltip="Remove this OR group">
                  <mat-icon style="font-size:16px">remove_circle</mat-icon>
                </button>
              </div>

              <!-- Group card -->
              <div style="border:1px solid #e0e0e0;border-radius:4px;padding:8px 10px;
                          margin-bottom:4px;background:#fafafa">
                <div *ngFor="let clause of group.clauses; let ci = index">
                  <!-- AND separator between clauses -->
                  <div *ngIf="ci > 0"
                       style="font-size:10px;font-weight:700;color:#1565c0;
                              margin:2px 0;text-align:center;letter-spacing:1px">
                    AND
                  </div>
                  <div style="display:flex;gap:6px;align-items:flex-start">
                    <mat-form-field style="flex:2;min-width:140px">
                      <mat-label>Policy Field</mat-label>
                      <input matInput [(ngModel)]="clause.path" [ngModelOptions]="{standalone:true}"
                             placeholder="$risk.X or $peril"
                             [matAutocomplete]="clausePathAuto">
                      <mat-autocomplete #clausePathAuto>
                        <mat-optgroup *ngFor="let grp of groupedFields" [label]="grp.category">
                          <mat-option *ngFor="let f of grp.fields" [value]="f.path">
                            <span>{{f.displayName}}</span>
                            <small style="color:#999;margin-left:6px">{{f.path}}</small>
                          </mat-option>
                        </mat-optgroup>
                      </mat-autocomplete>
                    </mat-form-field>
                    <mat-form-field style="flex:0 0 150px">
                      <mat-label>Operator</mat-label>
                      <mat-select [(ngModel)]="clause.operator" [ngModelOptions]="{standalone:true}">
                        <mat-option value="equals">equals</mat-option>
                        <mat-option value="notEquals">is not</mat-option>
                        <mat-option value="isTrue">is true</mat-option>
                        <mat-option value="greaterThan">is greater than</mat-option>
                        <mat-option value="lessThan">is less than</mat-option>
                        <mat-option value="greaterThanOrEqual">≥ is at least</mat-option>
                        <mat-option value="lessThanOrEqual">≤ is at most</mat-option>
                        <mat-option value="in">is one of (comma-separated)</mat-option>
                        <mat-option value="notIn">is not one of (comma-separated)</mat-option>
                      </mat-select>
                    </mat-form-field>
                    <mat-form-field style="flex:1;min-width:100px">
                      <mat-label>Value</mat-label>
                      <input matInput [(ngModel)]="clause.value" [ngModelOptions]="{standalone:true}"
                             placeholder="e.g. GRP1">
                    </mat-form-field>
                    <button mat-icon-button color="warn" type="button"
                            style="margin-top:4px;flex-shrink:0"
                            (click)="removeClause(gi, ci)"
                            [disabled]="group.clauses.length === 1 && whenGroups.length === 1"
                            matTooltip="Remove clause">
                      <mat-icon style="font-size:16px">remove_circle</mat-icon>
                    </button>
                  </div>
                </div>

                <button mat-button type="button" style="font-size:11px;height:26px;margin-top:2px"
                        (click)="addClause(gi)">
                  <mat-icon style="font-size:14px;width:14px;height:14px">add</mat-icon>
                  AND Condition
                </button>
              </div>
            </div>

            <button mat-stroked-button type="button"
                    style="margin-top:8px;font-size:11px;height:30px;border-color:#e65100;color:#e65100"
                    (click)="addOrGroup()">
              <mat-icon style="font-size:14px;width:14px;height:14px">add</mat-icon>
              Add OR Group
            </button>
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

  /** Full risk field list loaded from the registry — drives autocomplete across all path fields. */
  riskFields: RiskField[] = [];

  /** Fields grouped by category for mat-optgroup rendering. */
  get groupedFields(): { category: string; fields: RiskField[] }[] {
    const map = new Map<string, RiskField[]>();
    for (const f of this.riskFields) {
      const cat = f.category ?? 'Other';
      if (!map.has(cat)) map.set(cat, []);
      map.get(cat)!.push(f);
    }
    return Array.from(map.entries()).map(([category, fields]) => ({ category, fields }));
  }

  /** DNF group model for the compound When condition editor. */
  whenGroups: WhenGroup[] = [];
  hasWhen = false;

  private fb             = inject(FormBuilder);
  private riskFieldSvc   = inject(RiskFieldService);
  readonly dialogRef     = inject(MatDialogRef<StepFormComponent>);
  readonly data          = inject<StepFormData>(MAT_DIALOG_DATA);

  ngOnInit() {
    const productCode = this.data.productCode ?? '';
    this.riskFieldSvc.list(productCode).subscribe(f => this.riskFields = f);
    this.isEdit = !!this.data.step;
    const s = this.data.step;

    this.hasWhen    = !!s?.when;
    this.whenGroups = whenConfigToGroups(s?.when);

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
          s?.operation === 'lookup'
            ? Object.entries(s?.keys ?? {}).map(([k, v]) => this.newKeyGroup(k, v))
            : []
        ),
      }),

      adjustmentConfig: this.fb.group({
        mathType:      [s?.operation === 'adjustment' ? (s?.math?.type ?? 'mul') : 'mul'],
        sourceType:    [s?.sourceType ?? 'rateTable'],
        rateTable:     [s?.operation === 'adjustment' ? (s?.rateTable ?? '') : ''],
        constantValue: [s?.constantValue ?? null],
        stepOutputRef: [''],
        keys: this.fb.array(
          s?.operation === 'adjustment'
            ? Object.entries(s?.keys ?? {}).map(([k, v]) => this.newKeyGroup(k, v))
            : []
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

      outputAlias:    [s?.outputAlias    ?? ''],
      operationScope: [s?.operationScope ?? ''],
    });
  }

  toggleWhen(enabled: boolean) {
    this.hasWhen = enabled;
    if (enabled && this.whenGroups.length === 0)
      this.whenGroups = [{ clauses: [{ path: '', operator: 'equals', value: '' }] }];
  }

  addClause(groupIndex: number) {
    this.whenGroups[groupIndex].clauses.push({ path: '', operator: 'equals', value: '' });
    this.whenGroups = [...this.whenGroups];
  }

  removeClause(groupIndex: number, clauseIndex: number) {
    const group = this.whenGroups[groupIndex];
    if (group.clauses.length === 1 && this.whenGroups.length === 1) return;
    if (group.clauses.length === 1) {
      this.removeGroup(groupIndex);
    } else {
      group.clauses.splice(clauseIndex, 1);
      this.whenGroups = [...this.whenGroups];
    }
  }

  addOrGroup() {
    this.whenGroups = [...this.whenGroups, { clauses: [{ path: '', operator: 'equals', value: '' }] }];
  }

  removeGroup(groupIndex: number) {
    this.whenGroups = this.whenGroups.filter((_, i) => i !== groupIndex);
    if (this.whenGroups.length === 0)
      this.whenGroups = [{ clauses: [{ path: '', operator: 'equals', value: '' }] }];
  }

  get op(): string { return this.form.get('operation')?.value ?? ''; }
  get adjSource(): string { return this.form.get('adjustmentConfig.sourceType')?.value ?? 'rateTable'; }

  get keyControls(): AbstractControl[] {
    return (this.form.get('lookupConfig.keys') as FormArray).controls;
  }

  get adjKeyControls(): AbstractControl[] {
    return (this.form.get('adjustmentConfig.keys') as FormArray).controls;
  }

  /** Key names currently defined in the Lookup Keys list — drives the Interpolation Column dropdown. */
  get keyNames(): string[] {
    return this.keyControls
      .map(c => (c.get('keyName')?.value ?? '').trim())
      .filter((n): n is string => n.length > 0);
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

  addAdjKey() {
    (this.form.get('adjustmentConfig.keys') as FormArray).push(this.newKeyGroup());
  }

  removeAdjKey(i: number) {
    (this.form.get('adjustmentConfig.keys') as FormArray).removeAt(i);
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

      // Build keys record first so we can validate interpolateKey against it
      const keysArr: { keyName: string; keyValue: string }[] = lc.keys;
      if (keysArr.length > 0) {
        step.keys = Object.fromEntries(keysArr.map(k => [k.keyName, k.keyValue]));
      }

      if (lc.interpolateKey) {
        const definedNames = keysArr.map(k => k.keyName.trim());
        if (!definedNames.includes(lc.interpolateKey)) {
          // This should not happen via the dropdown, but guard against stale data
          this.form.get('lookupConfig.interpolateKey')?.setErrors({ notAKeyName: true });
          return;
        }
        step.interpolate = { key: lc.interpolateKey };
      }

      if (lc.rangeKey) step.rangeKey = { key: lc.rangeKey };
    }

    if (v.operation === 'adjustment') {
      const ac = v.adjustmentConfig;
      step.math       = { type: ac.mathType };
      step.sourceType = ac.sourceType;
      if (ac.sourceType === 'rateTable') {
        step.rateTable = ac.rateTable;
        const keysArr: { keyName: string; keyValue: string }[] = ac.keys;
        if (keysArr.length > 0)
          step.keys = Object.fromEntries(keysArr.map((k: { keyName: string; keyValue: string }) => [k.keyName, k.keyValue]));
      } else if (ac.sourceType === 'constant') {
        step.constantValue = ac.constantValue != null ? +ac.constantValue : undefined;
      }
      // stepOutput: no extra fields needed (engine reads from $risk.X via keys or stepOutputRef is informational)
    }

    if (v.operation === 'compute') {
      const cc = v.computeConfig;
      step.compute = { expr: cc.expr, storeAs: cc.storeAs, applyToPremium: cc.applyToPremium };
    }

    if (v.operation === 'round') {
      const rc = v.roundConfig;
      step.round = { precision: +rc.precision, mode: rc.mode };
    }

    // Compound When condition from DNF group model
    if (this.hasWhen) {
      const when = groupsToWhenConfig(this.whenGroups);
      if (when) step.when = when;
    }

    if (v.outputAlias?.trim())    step.outputAlias    = v.outputAlias.trim();
    if (v.operationScope?.trim()) step.operationScope = v.operationScope.trim();

    this.dialogRef.close(step);
  }
}
