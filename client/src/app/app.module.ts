import { HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';

import { AppComponent } from './app.component';
import { AlertFeedComponent } from './components/alert-feed/alert-feed.component';
import { LiveMapComponent } from './components/live-map/live-map.component';
import { TripPanelComponent } from './components/trip-panel/trip-panel.component';
import { VehicleListComponent } from './components/vehicle-list/vehicle-list.component';

@NgModule({
  declarations: [
    AppComponent,
    LiveMapComponent,
    VehicleListComponent,
    AlertFeedComponent,
    TripPanelComponent
  ],
  imports: [BrowserModule, HttpClientModule, FormsModule],
  bootstrap: [AppComponent]
})
export class AppModule {}
