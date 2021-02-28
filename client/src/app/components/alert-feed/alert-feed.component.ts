import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

import { Alert } from '../../models/fleet.models';

const ICONS: { [type: string]: string } = {
  GeofenceEnter: '→',
  GeofenceExit: '←',
  Overspeed: '⚡',
  Idle: '⏸'
};

@Component({
  selector: 'ft-alert-feed',
  templateUrl: './alert-feed.component.html',
  styleUrls: ['./alert-feed.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AlertFeedComponent {
  @Input() alerts: Alert[] = [];
  @Output() acknowledge = new EventEmitter<Alert>();
  @Output() focusVehicle = new EventEmitter<string>();

  iconFor(alert: Alert): string {
    return ICONS[alert.type] || '!';
  }

  trackById(_: number, a: Alert): string {
    return a.id;
  }
}
