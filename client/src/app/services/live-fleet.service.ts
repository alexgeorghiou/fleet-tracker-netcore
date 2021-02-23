import { Injectable, NgZone, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';

import { environment } from '../../environments/environment';
import { Alert, ConnectionState, PositionUpdate, TripNotification } from '../models/fleet.models';

/**
 * Thin RxJS wrapper over the SignalR fleet hub. Handlers are re-entered into the Angular zone so OnPush
 * components see updates without manual change detection.
 */
@Injectable({ providedIn: 'root' })
export class LiveFleetService implements OnDestroy {
  private readonly connection: HubConnection;
  private readonly state = new BehaviorSubject<ConnectionState>('disconnected');
  private readonly positions = new Subject<PositionUpdate>();
  private readonly alerts = new Subject<Alert>();
  private readonly trips = new Subject<TripNotification>();

  readonly state$: Observable<ConnectionState> = this.state.asObservable();
  readonly positions$: Observable<PositionUpdate> = this.positions.asObservable();
  readonly alerts$: Observable<Alert> = this.alerts.asObservable();
  readonly trips$: Observable<TripNotification> = this.trips.asObservable();

  constructor(private readonly zone: NgZone) {
    this.connection = new HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.on('PositionUpdated', (p: PositionUpdate) => this.zone.run(() => this.positions.next(p)));
    this.connection.on('AlertRaised', (a: Alert) => this.zone.run(() => this.alerts.next(a)));
    this.connection.on('TripChanged', (t: TripNotification) => this.zone.run(() => this.trips.next(t)));

    this.connection.onreconnecting(() => this.zone.run(() => this.state.next('reconnecting')));
    this.connection.onreconnected(() => this.zone.run(() => this.state.next('connected')));
    this.connection.onclose(() => this.zone.run(() => this.state.next('disconnected')));
  }

  async start(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }
    this.state.next('connecting');
    try {
      await this.connection.start();
      this.state.next('connected');
    } catch (err) {
      console.warn('Fleet hub connection failed, retrying in 5s', err);
      this.state.next('disconnected');
      setTimeout(() => this.start(), 5000);
    }
  }

  follow(vehicleId: string): Promise<void> {
    return this.connection.invoke('Follow', vehicleId);
  }

  unfollow(vehicleId: string): Promise<void> {
    return this.connection.invoke('Unfollow', vehicleId);
  }

  ngOnDestroy(): void {
    this.connection.stop();
  }
}
