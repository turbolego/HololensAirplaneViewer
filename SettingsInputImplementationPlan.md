# Settings Input Implementation Notes

## Constraint
The app is a native UWP HoloLens app whose entry point is the DirectX
`AppView` (`IFrameworkView`) — the XAML `App` object is never created. That
means the immersive holographic view cannot host XAML pages, `ContentDialog`,
or `TextBox` controls, and `CoreApplication.CreateNewView()` +
`Window.Current.Content = ...` fails at runtime. Free text entry would require
implementing TSF/UI Automation text providers on top of `CoreTextEditContext`,
which is out of scope.

`Windows.UI.Popups.MessageDialog`, however, is rendered by the shell over the
immersive view and already works (the airplane information modal uses it).

## Implemented design
Air-tapping the `[ SETTINGS ]` hologram opens a `MessageDialog` modal — the
same modal type as the airplane information dialog. `MessageDialog` supports
only command buttons (max 3), so the flow is a small chain of dialogs:

1. **Location Settings** — shows the active location and whether it is manual
   or automatic. Commands: `Pick a city`, `Adjust coordinates`, `Close`.
2. **Pick a City** — cycles through `LocationSettingsModel.Presets`, whose
   first entry restores the automatic (device supplied) location.
   Commands: `Use this location`, `Next`, `Back`.
3. **Adjust Coordinates** — pick `Latitude` or `Longitude`, then a step size
   (10 deg / 1 deg / 0.1 deg), then nudge with `+`/`-`. `Apply` stores the result.

## Code map
- `Content/AirplaneRenderer.cs` — draws the button and hit-tests the gaze ray.
  The hit sphere is compass-rotated exactly like the drawn label.
- `BasicHologramMain.cs` — runs the modal chain (`OpenSettingsView`).
- `Services/LocationSettingsModel.cs` — presets, coordinate stepping, and
  formatting (pure logic, unit tested).
- `Services/LocationOverrideStore.cs` — thread-safe bridge to the renderer;
  `Set` applies a manual location, `Clear` returns to the device location, and
  the generation counter invalidates in-flight OpenSky fetches.
