import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { ProductSummary } from '../../../../../core/models/api.models';

@Component({
  selector: 'app-product-timeline',
  templateUrl: './product-timeline.component.html'
})
export class ProductTimelineComponent implements OnChanges {
  @Input() versions: ProductSummary[] = [];
  @Input() selectedVersionId: number | null = null;
  
  @Output() versionSelected = new EventEmitter<ProductSummary>();

  sortedVersions: ProductSummary[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['versions'] && this.versions) {
      this.processVersions();
    }
  }

  private processVersions(): void {
    const today = new Date().toISOString().split('T')[0];

    this.sortedVersions = [...this.versions]
      .sort((a, b) => a.effStart.localeCompare(b.effStart))
      .map(v => {
        let status = v.status;
        if (!status) {
          if (v.expireAt && v.expireAt < today) status = 'Expired';
          else if (v.effStart > today) status = 'Draft';
          else status = 'Active';
        }
        return { ...v, status };
      });
  }

  selectVersion(version: ProductSummary): void {
    this.versionSelected.emit(version);
  }
}