export type VehicleStatus = 'Unknown' | 'Moving' | 'Idle' | 'Parked' | 'Offline';
export type AlertType = 'GeofenceEnter' | 'GeofenceExit' | 'Overspeed' | 'Idle';
export type AlertSeverity = 'Info' | 'Warning' | 'Critical';
export type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface LatLng {
  lat: number;
  lng: number;
}

export interface LastPosition {
  timestampUtc: string;
  latitude: number;
  longitude: number;
  speedKph: number;
  headingDegrees: number | null;
  ignitionOn: boolean;
}

export interface Vehicle {
  id: string;
  name: string;
  registrationPlate: string;
  deviceId: string;
  speedLimitKph: number;
  status: VehicleStatus;
  openTripId: string | null;
  lastPosition: LastPosition | null;
}

export interface Geofence {
  id: string;
  name: string;
  color: string;
  alertOnEnter: boolean;
  alertOnExit: boolean;
  isActive: boolean;
  areaSquareMeters: number;
  vertices: LatLng[];
}

export interface Trip {
  id: string;
  vehicleId: string;
  status: 'InProgress' | 'Completed';
  endReason: string | null;
  startedAtUtc: string;
  endedAtUtc: string | null;
  durationMinutes: number;
  distanceKm: number;
  maxSpeedKph: number;
  averageSpeedKph: number;
  start: LatLng;
  end: LatLng | null;
}

export interface Ping {
  timestampUtc: string;
  latitude: number;
  longitude: number;
  speedKph: number;
  headingDegrees: number | null;
  ignitionOn: boolean;
}

export interface TripDetail extends Trip {
  path: Ping[];
}

export interface Alert {
  id: string;
  vehicleId: string;
  vehicleName: string;
  type: AlertType;
  severity: AlertSeverity;
  message: string;
  raisedAtUtc: string;
  latitude: number;
  longitude: number;
  geofenceId: string | null;
  acknowledgedAtUtc?: string | null;
}

/** SignalR payloads (camelCase, mirrors FleetTracker.Core.Abstractions). */
export interface PositionUpdate {
  vehicleId: string;
  vehicleName: string;
  timestampUtc: string;
  latitude: number;
  longitude: number;
  speedKph: number;
  headingDegrees: number | null;
  ignitionOn: boolean;
  tripId: string | null;
}

export interface TripNotification {
  tripId: string;
  vehicleId: string;
  status: 'InProgress' | 'Completed';
  startedAtUtc: string;
  endedAtUtc: string | null;
  distanceMeters: number;
  endReason: string;
}
