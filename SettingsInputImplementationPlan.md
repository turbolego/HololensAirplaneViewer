# Settings Input Implementation Plan

## Research & Strategy
The application is a native UWP HoloLens 1 app using Direct3D 11 with SharpDX. There is no Unity3D/MRTK UI system. To provide a settings modal/popup for manual GPS address entry:

1.  **Architecture:** We must use **UWP XAML/D3D Interop**.
2.  **View Switching:** The application cannot render the virtual keyboard over the 3D view. We must use `CoreApplicationViewSwitcher` to transition from the volumetric (D3D) view to a 2D XAML view when input is requested.
3.  **Keyboard Input:** In the 2D XAML view, we will use a standard `TextBox` control, which automatically triggers the system-wide virtual keyboard on HoloLens.
4.  **Data Flow:**
    *   Volumetric view -> Trigger input -> Transition to XAML Page.
    *   XAML Page -> User types in `TextBox` -> Capture result.
    *   XAML Page -> Transition back to Volumetric view -> Pass result to `AirplaneService`.

## Implementation Plan
1.  **Project Modification:** Add a new XAML page (`SettingsPage.xaml`) to the project.
2.  **View Switching Logic:** Implement `CoreApplicationViewSwitcher` in the main app lifecycle (`BasicHologramMain.cs` or similar) to handle the transition.
3.  **UI/UX:** Define a 3D button in the volumetric view (custom rendered) to trigger the input switch.
4.  **Data Integration:** Create a communication bridge to pass the entered address back to the `GeolocationService` or `AirplaneService`.

## Known Constraints
- This requires low-level UWP/Direct3D interop development.
- The volumetric renderer must be paused or handled correctly during the transition to the 2D view.
