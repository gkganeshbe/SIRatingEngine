import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { RiskField } from '../../../../core/models/api.models';

export interface RiskFieldGroup {
  category: string;
  fields: RiskField[];
}

@Component({
  selector: 'app-risk-field-picker',
  templateUrl: './risk-field-picker.component.html'
})
export class RiskFieldPickerComponent implements OnChanges {
  @Input() riskFields: RiskField[] = [];
  @Input() value: string | undefined | null = '';
  @Input() label: string = 'Select Risk Attribute';
  
  @Output() valueChange = new EventEmitter<string>();

  groupedFields: RiskFieldGroup[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['riskFields'] && this.riskFields) {
      this.buildGroups();
    }
  }

  private buildGroups(): void {
    const map = new Map<string, RiskField[]>();

    this.riskFields.forEach(f => {
      const cat = f.category || 'Uncategorized';
      if (!map.has(cat)) map.set(cat, []);
      map.get(cat)!.push(f);
    });

    this.groupedFields = Array.from(map.entries())
      .map(([category, fields]) => ({
        category,
        fields: fields.sort((a, b) => a.sortOrder - b.sortOrder)
      }))
      .sort((a, b) => a.category.localeCompare(b.category));
  }

  onSelectionChange(newValue: string): void {
    this.value = newValue;
    this.valueChange.emit(this.value);
  }
}