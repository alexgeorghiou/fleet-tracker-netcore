import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

import { VehicleView } from '../../services/fleet-store.service';

@Component({
  selector: 'ft-vehicle-list',
  templateUrl: './vehicle-list.component.html',
  styleUrls: ['./vehicle-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VehicleListComponent {
  @Input() vehicles: VehicleView[] = [];
  @Input() selectedId: string | null = null;
  @Output() selected = new EventEmitter<string>();

  filter = '';

  get visible(): VehicleView[] {
    const f = this.filter.trim().toLowerCase();
    if (!f) {
      return this.vehicles;
    }
    return this.vehicles.filter(v =>
      v.name.toLowerCase().indexOf(f) >= 0 || (v.registrationPlate || '').toLowerCase().indexOf(f) >= 0);
  }

  trackById(_: number, v: VehicleView): string {
    return v.id;
  }

  isOverLimit(v: VehicleView): boolean {
    return !!v.lastPosition && v.lastPosition.speedKph > v.speedLimitKph;
  }
}
