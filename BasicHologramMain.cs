//
// Comment out this preprocessor definition to disable all of the
// sample content.
//
// To remove the content after disabling it:
//     * Remove the unused code from this file.
//     * Delete the Content folder provided with this template.
//
#define DRAW_SAMPLE_CONTENT

using System;
using System.Diagnostics;
using Windows.Gaming.Input;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Holographic;
using Windows.Perception.Spatial;
using Windows.UI.Input.Spatial;
using Windows.UI.Popups;
using Windows.Storage;

using HololensAirplaneViewer.Common;
using HololensAirplaneViewer.Models;
using HololensAirplaneViewer.Services;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Collections.Generic;

#if DRAW_SAMPLE_CONTENT
using HololensAirplaneViewer.Content;
#endif

namespace HololensAirplaneViewer
{
    /// <summary>
    /// Updates, renders, and presents holographic content using Direct3D.
    /// </summary>
    internal class AirplaneViewerMain : IDisposable
    {

#if DRAW_SAMPLE_CONTENT
        // Renders airplaneRenderers as holograms positioned in world space
        // relative to user's GPS location and Earth orbit.
        private AirplaneRenderer airplaneRenderer;

        private SpatialInputHandler spatialInputHandler;
        private CompassService compassService;
#endif

        // Cached reference to device resources.
        private DeviceResources deviceResources;

        // Render loop timer.
        private StepTimer timer = new StepTimer();

        // Represents the holographic space around the user.
        HolographicSpace holographicSpace;

        // SpatialLocator that is attached to the default HolographicDisplay.
        SpatialLocator spatialLocator;

        // A stationary reference frame based on spatialLocator.
        SpatialStationaryFrameOfReference stationaryReferenceFrame;

        // Keep track of gamepads.
        private class GamepadWithButtonState
        {
            public Windows.Gaming.Input.Gamepad gamepad;
            public bool buttonAWasPressedLastFrame;
            public GamepadWithButtonState(
                Windows.Gaming.Input.Gamepad gamepad,
                bool buttonAWasPressedLastFrame)
            {
                this.gamepad = gamepad;
                this.buttonAWasPressedLastFrame = buttonAWasPressedLastFrame;
            }
        };
        List<GamepadWithButtonState> gamepads = new List<GamepadWithButtonState>();

        // Keep track of mouse input.
        bool pointerPressed = false;

        // Guard against stacking airplane info dialogs.
        private bool _infoDialogShowing = false;
        private bool _settingsDialogShowing = false;

        // Cache whether or not the HolographicCamera.Display property can be accessed.
        bool canGetHolographicDisplayForCamera = false;

        // Cache whether or not the HolographicDisplay.GetDefault() method can be called.
        bool canGetDefaultHolographicDisplay = false;

        // Cache whether or not the HolographicCameraRenderingParameters.CommitDirect3D11DepthBuffer() method can be called.
        bool canCommitDirect3D11DepthBuffer = false;

        /// <summary>
        /// Loads and initializes application assets when the application is loaded.
        /// </summary>
        /// <param name="deviceResources"></param>
        public AirplaneViewerMain(DeviceResources deviceResources)
        {
            this.deviceResources = deviceResources;

            // Register to be notified if the Direct3D device is lost.
            this.deviceResources.DeviceLost += this.OnDeviceLost;
            this.deviceResources.DeviceRestored += this.OnDeviceRestored;

            // If connected, a game controller can also be used for input.
            Gamepad.GamepadAdded += this.OnGamepadAdded;
            Gamepad.GamepadRemoved += this.OnGamepadRemoved;

            foreach (var gamepad in Gamepad.Gamepads)
            {
                OnGamepadAdded(null, gamepad);
            }

            canGetHolographicDisplayForCamera = Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Holographic.HolographicCamera", "Display");
            canGetDefaultHolographicDisplay = Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Graphics.Holographic.HolographicDisplay", "GetDefault");
            canCommitDirect3D11DepthBuffer = Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Graphics.Holographic.HolographicCameraRenderingParameters", "CommitDirect3D11DepthBuffer");
        }

        public void SetHolographicSpace(HolographicSpace holographicSpace)
        {
            this.holographicSpace = holographicSpace;

            // 
            // TODO: Add code here to initialize your content.
            // 

#if DRAW_SAMPLE_CONTENT
            // Initialize the sample hologram.
            airplaneRenderer = new AirplaneRenderer(deviceResources);

            spatialInputHandler = new SpatialInputHandler();

            // Initialize compass for heading-based dome rotation
            compassService = new CompassService();
            compassService.Initialize();
#endif

            if (canGetDefaultHolographicDisplay)
            {
                // Subscribe for notifications about changes to the state of the default HolographicDisplay 
                // and its SpatialLocator.
                HolographicSpace.IsAvailableChanged += (sender, args) => this.OnHolographicDisplayIsAvailableChanged((HolographicSpace)sender, args);
            }

            // Acquire the current state of the default HolographicDisplay and its SpatialLocator.
            OnHolographicDisplayIsAvailableChanged(null, null);

            // Respond to camera added events by creating any resources that are specific
            // to that camera, such as the back buffer render target view.
            // When we add an event handler for CameraAdded, the API layer will avoid putting
            // the new camera in new HolographicFrames until we complete the deferral we created
            // for that handler, or return from the handler without creating a deferral. This
            // allows the app to take more than one frame to finish creating resources and
            // loading assets for the new holographic camera.
            // This function should be registered before the app creates any HolographicFrames.
            holographicSpace.CameraAdded += this.OnCameraAdded;

            // Respond to camera removed events by releasing resources that were created for that
            // camera.
            // When the app receives a CameraRemoved event, it releases all references to the back
            // buffer right away. This includes render target views, Direct2D target bitmaps, and so on.
            // The app must also ensure that the back buffer is not attached as a render target, as
            // shown in DeviceResources.ReleaseResourcesForBackBuffer.
            holographicSpace.CameraRemoved += this.OnCameraRemoved;

            // Notes on spatial tracking APIs:
            // * Stationary reference frames are designed to provide a best-fit position relative to the
            //   overall space. Individual positions within that reference frame are allowed to drift slightly
            //   as the device learns more about the environment.
            // * When precise placement of individual holograms is required, a SpatialAnchor should be used to
            //   anchor the individual hologram to a position in the real world - for example, a point the user
            //   indicates to be of special interest. Anchor positions do not drift, but can be corrected; the
            //   anchor will use the corrected position starting in the next frame after the correction has
            //   occurred.
        }

        public void Dispose()
        {
#if DRAW_SAMPLE_CONTENT
            if (airplaneRenderer != null)
            {
                airplaneRenderer.Dispose();
                airplaneRenderer = null;
            }
#endif
            if (compassService != null)
            {
                compassService.Dispose();
                compassService = null;
            }
        }

        /// <summary>
        /// Updates the application state once per frame.
        /// </summary>
        public HolographicFrame Update()
        {
            // Before doing the timer update, there is some work to do per-frame
            // to maintain holographic rendering. First, we will get information
            // about the current frame.

            // The HolographicFrame has information that the app needs in order
            // to update and render the current frame. The app begins each new
            // frame by calling CreateNextFrame.
            HolographicFrame holographicFrame = holographicSpace.CreateNextFrame();

            // Get a prediction of where holographic cameras will be when this frame
            // is presented.
            HolographicFramePrediction prediction = holographicFrame.CurrentPrediction;

            // Back buffers can change from frame to frame. Validate each buffer, and recreate
            // resource views and depth buffers as needed.
            deviceResources.EnsureCameraResources(holographicFrame, prediction);

#if DRAW_SAMPLE_CONTENT
            if (stationaryReferenceFrame != null)
            {
                // Check for new input state since the last frame.
                for (int i = 0; i < gamepads.Count; ++i)
                {
                    bool buttonDownThisUpdate = (gamepads[i].gamepad.GetCurrentReading().Buttons & GamepadButtons.A) == GamepadButtons.A;
                    if (buttonDownThisUpdate && !gamepads[i].buttonAWasPressedLastFrame)
                    {
                        pointerPressed = true;
                    }
                    gamepads[i].buttonAWasPressedLastFrame = buttonDownThisUpdate;
                }

                SpatialInteractionSourceState pointerState = spatialInputHandler.CheckForInput();
                if (pointerState != null)
                {
                    pointerPressed = true;
                }
                
                // Always obtain the current head pose every frame
                SpatialPointerPose headPose = SpatialPointerPose.TryGetAtTimestamp(
                    stationaryReferenceFrame.CoordinateSystem, prediction.Timestamp);

                if (pointerPressed && !_settingsDialogShowing && !_infoDialogShowing
                    && airplaneRenderer.CheckSettingsHit(headPose))
                {
                    OpenSettingsView();
                }
                else if (pointerPressed && !_infoDialogShowing && !_settingsDialogShowing)
                {
                    var hitPlane = airplaneRenderer.CheckAirplaneHit(headPose);
                    if (hitPlane != null)
                    {
                        ShowAirplaneInfoDialog(hitPlane);
                    }
                }

                pointerPressed = false;

                // Read the latest compass heading (updated on background thread by CompassService)
                float compassHeading = compassService?.CalibratedHeading ?? 0f;

                airplaneRenderer.PositionHologram(headPose);
                airplaneRenderer.SetCompassHeading(compassHeading);
            }
#endif

            timer.Tick(() =>
            {
                //
                // TODO: Update scene objects.
                //
                // Put time-based updates here. By default this code will run once per frame,
                // but if you change the StepTimer to use a fixed time step this code will
                // run as many times as needed to get to the current step.
                //

#if DRAW_SAMPLE_CONTENT
                airplaneRenderer.Update(timer);
#endif
            });

            if (!canCommitDirect3D11DepthBuffer)
            {
                // On versions of the platform that do not support the CommitDirect3D11DepthBuffer API, we can control
                // image stabilization by setting a focus point with optional plane normal and velocity.
                foreach (var cameraPose in prediction.CameraPoses)
                {
#if DRAW_SAMPLE_CONTENT
                    // The HolographicCameraRenderingParameters class provides access to set
                    // the image stabilization parameters.
                    HolographicCameraRenderingParameters renderingParameters = holographicFrame.GetRenderingParameters(cameraPose);

                    // SetFocusPoint informs the system about a specific point in your scene to
                    // prioritize for image stabilization. The focus point is set independently
                    // for each holographic camera. When setting the focus point, put it on or 
                    // near content that the user is looking at.
                    // In this example, we put the focus point at the center of the sample hologram.
                    // You can also set the relative velocity and facing of the stabilization
                    // plane using overloads of this method.
                    if (stationaryReferenceFrame != null)
                    {
                        renderingParameters.SetFocusPoint(
                            stationaryReferenceFrame.CoordinateSystem,
                            airplaneRenderer.Position
                            );
                    }
#endif
                }
            }

            // The holographic frame will be used to get up-to-date view and projection matrices and
            // to present the swap chain.
            return holographicFrame;
        }

        /// <summary>
        /// Renders the current frame to each holographic display, according to the 
        /// current application and spatial positioning state. Returns true if the 
        /// frame was rendered to at least one display.
        /// </summary>
        public bool Render(HolographicFrame holographicFrame)
        {
            // Don't try to render anything before the first Update.
            if (timer.FrameCount == 0)
            {
                return false;
            }

            //
            // TODO: Add code for pre-pass rendering here.
            //
            // Take care of any tasks that are not specific to an individual holographic
            // camera. This includes anything that doesn't need the final view or projection
            // matrix, such as lighting maps.
            //

            // Up-to-date frame predictions enhance the effectiveness of image stablization and
            // allow more accurate positioning of holograms.
            holographicFrame.UpdateCurrentPrediction();
            HolographicFramePrediction prediction = holographicFrame.CurrentPrediction;

            // Lock the set of holographic camera resources, then draw to each camera
            // in this frame.
            return deviceResources.UseHolographicCameraResources(
                (Dictionary<uint, CameraResources> cameraResourceDictionary) =>
                {
                    bool atLeastOneCameraRendered = false;

                    foreach (var cameraPose in prediction.CameraPoses)
                    {
                        // This represents the device-based resources for a HolographicCamera.
                        CameraResources cameraResources = cameraResourceDictionary[cameraPose.HolographicCamera.Id];

                        // Get the device context.
                        var context = deviceResources.D3DDeviceContext;
                        var renderTargetView = cameraResources.BackBufferRenderTargetView;
                        var depthStencilView = cameraResources.DepthStencilView;

                        // Set render targets to the current holographic camera.
                        context.OutputMerger.SetRenderTargets(depthStencilView, renderTargetView);

                        // Clear the back buffer and depth stencil view.
                        if (canGetHolographicDisplayForCamera &&
                            cameraPose.HolographicCamera.Display.IsOpaque)
                        {
                            SharpDX.Mathematics.Interop.RawColor4 cornflowerBlue = new SharpDX.Mathematics.Interop.RawColor4(0.392156899f, 0.58431375f, 0.929411829f, 1.0f);
                            context.ClearRenderTargetView(renderTargetView, cornflowerBlue);
                        }
                        else
                        {
                            SharpDX.Mathematics.Interop.RawColor4 transparent = new SharpDX.Mathematics.Interop.RawColor4(0.0f, 0.0f, 0.0f, 0.0f);
                            context.ClearRenderTargetView(renderTargetView, transparent);
                        }
                        context.ClearDepthStencilView(
                            depthStencilView,
                            SharpDX.Direct3D11.DepthStencilClearFlags.Depth | SharpDX.Direct3D11.DepthStencilClearFlags.Stencil,
                            1.0f,
                            0);

                        //
                        // TODO: Replace the sample content with your own content.
                        //
                        // Notes regarding holographic content:
                        //    * For drawing, remember that you have the potential to fill twice as many pixels
                        //      in a stereoscopic render target as compared to a non-stereoscopic render target
                        //      of the same resolution. Avoid unnecessary or repeated writes to the same pixel,
                        //      and only draw holograms that the user can see.
                        //    * To help occlude hologram geometry, you can create a depth map using geometry
                        //      data obtained via the surface mapping APIs. You can use this depth map to avoid
                        //      rendering holograms that are intended to be hidden behind tables, walls,
                        //      monitors, and so on.
                        //    * On HolographicDisplays that are transparent, black pixels will appear transparent 
                        //      to the user. On such devices, you should clear the screen to Transparent as shown 
                        //      above. You should still use alpha blending to draw semitransparent holograms. 
                        //


                        // The view and projection matrices for each holographic camera will change
                        // every frame. This function refreshes the data in the constant buffer for
                        // the holographic camera indicated by cameraPose.
                        if (stationaryReferenceFrame != null)
                        {
                            cameraResources.UpdateViewProjectionBuffer(deviceResources, cameraPose, stationaryReferenceFrame.CoordinateSystem);
                        }

                        // Attach the view/projection constant buffer for this camera to the graphics pipeline.
                        bool cameraActive = cameraResources.AttachViewProjectionBuffer(deviceResources);

#if DRAW_SAMPLE_CONTENT
                        // Only render world-locked content when positional tracking is active.
                        if (cameraActive)
                        {
                            // Draw the sample hologram.
                            airplaneRenderer.Render();

                            if (canCommitDirect3D11DepthBuffer)
                            {
                                // On versions of the platform that support the CommitDirect3D11DepthBuffer API, we can 
                                // provide the depth buffer to the system, and it will use depth information to stabilize 
                                // the image at a per-pixel level.
                                HolographicCameraRenderingParameters renderingParameters = holographicFrame.GetRenderingParameters(cameraPose);
                                SharpDX.Direct3D11.Texture2D depthBuffer = cameraResources.DepthBufferTexture2D;

                                // Direct3D interop APIs are used to provide the buffer to the WinRT API.
                                SharpDX.DXGI.Resource1 depthStencilResource = depthBuffer.QueryInterface<SharpDX.DXGI.Resource1>();
                                SharpDX.DXGI.Surface2 depthDxgiSurface = new SharpDX.DXGI.Surface2(depthStencilResource, 0);
                                IDirect3DSurface depthD3DSurface = InteropStatics.CreateDirect3DSurface(depthDxgiSurface.NativePointer);
                                if (depthD3DSurface != null)
                                {
                                    // Calling CommitDirect3D11DepthBuffer causes the system to queue Direct3D commands to 
                                    // read the depth buffer. It will then use that information to stabilize the image as
                                    // the HolographicFrame is presented.
                                    renderingParameters.CommitDirect3D11DepthBuffer(depthD3DSurface);
                                }
                            }
                        }
#endif
                        atLeastOneCameraRendered = true;
                    }

                    return atLeastOneCameraRendered;
                });
        }

        public void SaveAppState()
        {
            //
            // TODO: Insert code here to save your app state.
            //       This method is called when the app is about to suspend.
            //
            //       For example, store information in the SpatialAnchorStore.
            //
        }

        public void LoadAppState()
        {
            //
            // TODO: Insert code here to load your app state.
            //       This method is called when the app resumes.
            //
            //       For example, load information from the SpatialAnchorStore.
            //
        }

        public void OnPointerPressed()
        {
            this.pointerPressed = true;
        }

        /// <summary>
        /// Notifies renderers that device resources need to be released.
        /// </summary>
        public void OnDeviceLost(Object sender, EventArgs e)
        {

#if DRAW_SAMPLE_CONTENT
            airplaneRenderer.ReleaseDeviceDependentResources();
#endif

        }

        /// <summary>
        /// Notifies renderers that device resources may now be recreated.
        /// </summary>
        public void OnDeviceRestored(Object sender, EventArgs e)
        {
#if DRAW_SAMPLE_CONTENT
            airplaneRenderer.CreateDeviceDependentResourcesAsync();
#endif
        }

        void OnLocatabilityChanged(SpatialLocator sender, Object args)
        {
            switch (sender.Locatability)
            {
                case SpatialLocatability.Unavailable:
                    // Holograms cannot be rendered.
                    {
                        String message = "Warning! Positional tracking is " + sender.Locatability + ".";
                        Debug.WriteLine(message);
                    }
                    break;

                // In the following three cases, it is still possible to place holograms using a
                // SpatialLocatorAttachedFrameOfReference.
                case SpatialLocatability.PositionalTrackingActivating:
                // The system is preparing to use positional tracking.

                case SpatialLocatability.OrientationOnly:
                // Positional tracking has not been activated.

                case SpatialLocatability.PositionalTrackingInhibited:
                    // Positional tracking is temporarily inhibited. User action may be required
                    // in order to restore positional tracking.
                    break;

                case SpatialLocatability.PositionalTrackingActive:
                    // Positional tracking is active. World-locked content can be rendered.
                    break;
            }
        }

        public void OnCameraAdded(
            HolographicSpace sender,
            HolographicSpaceCameraAddedEventArgs args)
        {
            // Deferral helps to keep the app responsive.
            var deferral = args.GetDeferral();

            // Create camera-specific resources.
            deviceResources.AddHolographicCamera(args.Camera);

            // Complete the deferral.
            deferral.Complete();
        }

        public void OnCameraRemoved(
            HolographicSpace sender,
            HolographicSpaceCameraRemovedEventArgs args)
        {
            // Release camera-specific resources.
            deviceResources.RemoveHolographicCamera(args.Camera);
        }

        public void OnGamepadAdded(Object sender, Gamepad gamepad)
        {
            gamepads.Add(new GamepadWithButtonState(gamepad, false));
        }

        public void OnGamepadRemoved(Object sender, Gamepad gamepad)
        {
            for (int i = 0; i < gamepads.Count; i++)
            {
                if (gamepads[i].gamepad == gamepad)
                {
                    gamepads.RemoveAt(i);
                    break;
                }
            }
        }

        public void OnHolographicDisplayIsAvailableChanged(HolographicSpace sender, Object args)
        {
            // Get the default holographic display for the current view.
            HolographicDisplay holographicDisplay = HolographicDisplay.GetDefault();

            if (holographicDisplay != null)
            {
                spatialLocator = holographicDisplay.SpatialLocator;
            }
            else
            {
                spatialLocator = null;
            }

            if (spatialLocator != null)
            {
                stationaryReferenceFrame = spatialLocator.CreateStationaryFrameOfReferenceAtCurrentLocation();
            }
            else
            {
                stationaryReferenceFrame = null;
            }

#if DRAW_SAMPLE_CONTENT
            // Propagate the stationary reference frame to the renderer
            airplaneRenderer?.SetStationaryReferenceFrame(stationaryReferenceFrame);
#endif
        }

        private async void OpenSettingsView()
        {
            if (_settingsDialogShowing || _infoDialogShowing)
            {
                return;
            }

            _settingsDialogShowing = true;

            try
            {
                await RunLocationSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Dialog error: {ex.Message}");
            }
            finally
            {
                _settingsDialogShowing = false;
            }
        }

        /// <summary>
        /// Drives the location settings modal. The immersive holographic view
        /// cannot host XAML text boxes, so the location is changed with
        /// MessageDialog command buttons (the same modal type used by the
        /// airplane information dialog).
        /// </summary>
        private async Task RunLocationSettingsAsync()
        {
            double latitude;
            double longitude;
            bool manual = LocationOverrideStore.TryGet(out latitude, out longitude);
            bool hasFix = manual || airplaneRenderer.HasObserverFix;
            if (!manual)
            {
                latitude = airplaneRenderer.CurrentLatitude;
                longitude = airplaneRenderer.CurrentLongitude;
            }

            while (true)
            {
                string status = string.Format(
                    "{0}\n{1}",
                    manual ? "Manual location" : "Automatic (device) location",
                    LocationSettingsModel.FormatCoordinates(latitude, longitude));

                int choice;
                string dialogStatus = hasFix
                    ? status
                    : "Waiting for device location. Choose a city or enter geohash to set an initial location.";
                choice = hasFix
                    ? await ShowChoiceDialogAsync(
                        dialogStatus,
                        "Location Settings",
                        "Change location",
                        "Set North",
                        "Close")
                    : await ShowChoiceDialogAsync(
                        dialogStatus,
                        "Location Settings",
                        "Change location",
                        "Close");

                if (choice == 0)
                {
                    await RunLocationActionsAsync(latitude, longitude, hasFix);
                }
                else if (choice == 1 && hasFix)
                {
                    SpatialPointerPose alignmentPose = stationaryReferenceFrame == null
                        ? null
                        : SpatialPointerPose.TryGetAtTimestamp(
                            stationaryReferenceFrame.CoordinateSystem,
                            Windows.Perception.PerceptionTimestampHelper.FromHistoricalTargetTime(DateTime.Now));
                    if (alignmentPose == null)
                    {
                        await ShowChoiceDialogAsync(
                            "The current head direction is unavailable. Try setting north again.",
                            "Set North",
                            "OK");
                        continue;
                    }

                    var forward = alignmentPose.Head.ForwardDirection;
                    double horizontalLength = Math.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
                    if (horizontalLength < 0.1)
                    {
                        await ShowChoiceDialogAsync(
                            "Look toward the horizon before setting north.",
                            "Set North",
                            "OK");
                        continue;
                    }

                    float northAlignment = (float)(Math.Atan2(forward.X, -forward.Z) * 180.0 / Math.PI);
                    float magneticHeading = compassService?.CurrentHeadingDegrees ?? 0f;
                    compassService?.SetCalibrationOffset(magneticHeading);
                    airplaneRenderer.SetNorthAlignment(northAlignment);
                    await ShowChoiceDialogAsync(
                        string.Format(
                            "North aligned to your gaze. Magnetic correction saved: {0:F1}°.",
                            magneticHeading),
                        "Set North",
                        "OK");
                    // Continue loop to allow further adjustments
                }
                else
                {
                    return;
                }

                manual = LocationOverrideStore.TryGet(out latitude, out longitude);
                hasFix = manual || airplaneRenderer.HasObserverFix;
                if (!manual)
                {
                    latitude = airplaneRenderer.CurrentLatitude;
                    longitude = airplaneRenderer.CurrentLongitude;
                }
            }
        }

        private async Task RunLocationActionsAsync(double latitude, double longitude, bool hasFix)
        {
            int choice = await ShowChoiceDialogAsync(
                "Choose how to set the location.",
                "Location Options",
                "Pick a city",
                "Enter location",
                "Back");

            if (choice == 0)
            {
                await PickPresetLocationAsync();
            }
            else if (choice == 1)
            {
                await EnterLocationMenuAsync(latitude, longitude, hasFix);
            }
        }

        private async Task EnterLocationMenuAsync(double latitude, double longitude, bool hasFix)
        {
            if (!hasFix)
            {
                int initialChoice = await ShowChoiceDialogAsync(
                    "No device or manual location is available yet.",
                    "Set Initial Location",
                    "Pick a city",
                    "Sample geohash",
                    "Back");
                if (initialChoice == 0)
                {
                    await PickPresetLocationAsync();
                }
                else if (initialChoice == 1)
                {
                    await PickGeohashExampleAsync(latitude, longitude);
                }
                return;
            }

            while (true)
            {
                int choice = await ShowChoiceDialogAsync(
                    LocationSettingsModel.FormatCoordinates(latitude, longitude),
                    "Enter Location",
                    "Nearby airports",
                    "More location options",
                    "Back");
                if (choice == 0)
                {
                    await PickNearbyAirportAsync(latitude, longitude);
                    return;
                }
                if (choice == 1)
                {
                    int locationChoice = await ShowChoiceDialogAsync(
                        "Choose how to set the location.",
                        "Location Options",
                        "Adjust coordinates",
                        "Sample geohash",
                        "Back");
                    if (locationChoice == 0)
                    {
                        await AdjustCoordinatesAsync(latitude, longitude);
                        return;
                    }
                    if (locationChoice == 1)
                    {
                        await PickGeohashExampleAsync(latitude, longitude);
                        return;
                    }
                    continue;
                }

                return;
            }
        }

        private async Task PickGeohashExampleAsync(double latitude, double longitude)
        {
            var demoHashes = new string[] { "u4pruydqqvj", "u4pruyd", "u4pruy" };
            int idx = 0;
            while (true)
            {
                string hash = demoHashes[idx];
                if (!LocationSettingsModel.TryDecodeGeohash(hash, out double decodedLatitude, out double decodedLongitude))
                {
                    await ShowChoiceDialogAsync(
                        string.Format("The sample geohash '{0}' could not be decoded.", hash),
                        "Invalid Sample Geohash",
                        "OK");
                    return;
                }

                int c = await ShowChoiceDialogAsync(
                    string.Format("Geohash: {0}\n{1}", hash, LocationSettingsModel.FormatCoordinates(decodedLatitude, decodedLongitude)),
                    "Sample Geohash",
                    "Use this geohash",
                    "Next",
                    "Back");
                if (c == 0)
                {
                    LocationOverrideStore.Set(decodedLatitude, decodedLongitude);
                    return;
                }
                if (c == 1)
                {
                    idx = (idx + 1) % demoHashes.Length;
                    continue;
                }
                return;
            }
        }

        private async Task PickPresetLocationAsync()
        {
            var presets = LocationSettingsModel.Presets;
            int index = 0;

            while (true)
            {
                var preset = presets[index];
                string detail = preset.IsDeviceLocation
                    ? "Use the location reported by the device."
                    : LocationSettingsModel.FormatCoordinates(preset.Latitude, preset.Longitude);

                int choice = await ShowChoiceDialogAsync(
                    string.Format("{0}\n{1}", preset.Name, detail),
                    string.Format("Pick a City ({0}/{1})", index + 1, presets.Count),
                    "Use this location",
                    "Next",
                    "Back");

                if (choice == 0)
                {
                    if (preset.IsDeviceLocation)
                    {
                        LocationOverrideStore.Clear();
                    }
                    else
                    {
                        LocationOverrideStore.Set(preset.Latitude, preset.Longitude);
                    }
                    return;
                }
                if (choice == 1)
                {
                    index = LocationSettingsModel.NextPresetIndex(index);
                    continue;
                }

                return;
            }
        }

        private async Task PickNearbyAirportAsync(double latitude, double longitude)
        {
            AirportLocation[] airports;
            try
            {
                StorageFile catalogFile = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Services/airports.tsv"));
                IList<string> catalogLines = await FileIO.ReadLinesAsync(catalogFile);
                airports = LocationSettingsModel.FindThreeClosestAirports(
                    LocationSettingsModel.ParseAirportCatalog(catalogLines),
                    latitude,
                    longitude);
            }
            catch (Exception exception)
            {
                await ShowChoiceDialogAsync(
                    string.Format("Airport data could not be loaded: {0}", exception.Message),
                    "Nearby Airports",
                    "OK");
                return;
            }

            if (airports.Length == 0)
            {
                await ShowChoiceDialogAsync(
                    "No airports are available in the airport catalog.",
                    "Nearby Airports",
                    "OK");
                return;
            }

            int index = 0;
            while (true)
            {
                var a = airports[index];
                string detail = string.Format("{0} {1}\n{2}", a.Iata, a.Name, LocationSettingsModel.FormatCoordinates(a.Latitude, a.Longitude));
                int choice = await ShowChoiceDialogAsync(
                    detail,
                    string.Format("Nearby Airports ({0}/{1})", index + 1, airports.Length),
                    "Use this airport",
                    "Next",
                    "Back");

                if (choice == 0)
                {
                    LocationOverrideStore.Set(a.Latitude, a.Longitude);
                    return;
                }
                if (choice == 1)
                {
                    index = (index + 1) % airports.Length;
                    continue;
                }
                return;
            }
        }

        private async Task AdjustCoordinatesAsync(double latitude, double longitude)
        {
            while (true)
            {
                int choice = await ShowChoiceDialogAsync(
                    LocationSettingsModel.FormatCoordinates(latitude, longitude),
                    "Adjust Coordinates",
                    "Latitude",
                    "Longitude",
                    "Apply");

                if (choice == 2)
                {
                    LocationOverrideStore.Set(latitude, longitude);
                    return;
                }

                if (choice < 0)
                {
                    return;
                }

                bool editingLatitude = choice == 0;
                double step = await PickStepAsync();
                if (step <= 0.0)
                {
                    continue;
                }

                if (editingLatitude)
                {
                    latitude = await NudgeCoordinateAsync(true, step, latitude, longitude);
                }
                else
                {
                    longitude = await NudgeCoordinateAsync(false, step, latitude, longitude);
                }
            }
        }

        /// <summary>
        /// Asks for the step size used when nudging a coordinate.
        /// Returns 0 when the user dismissed the dialog.
        /// </summary>
        private async Task<double> PickStepAsync()
        {
            var steps = LocationSettingsModel.StepSizesDegrees;
            int choice = await ShowChoiceDialogAsync(
                "How far should each step move the location?",
                "Step Size",
                LocationSettingsModel.FormatStep(steps[0]),
                LocationSettingsModel.FormatStep(steps[1]),
                "More precise");

            if (choice < 0) return 0.0;
            if (choice < 2) return steps[choice];

            choice = await ShowChoiceDialogAsync(
                "How far should each step move the location?",
                "Step Size",
                LocationSettingsModel.FormatStep(steps[2]),
                LocationSettingsModel.FormatStep(steps[3]),
                "More precise");

            if (choice < 0) return 0.0;
            if (choice < 2) return steps[choice + 2];

            choice = await ShowChoiceDialogAsync(
                "How far should each step move the location?",
                "Step Size",
                LocationSettingsModel.FormatStep(steps[4]),
                LocationSettingsModel.FormatStep(steps[5]),
                "Back");

            return choice < 0 || choice == 2 ? 0.0 : steps[choice + 4];
        }

        /// <summary>
        /// Repeatedly nudges one coordinate and returns its final value.
        /// </summary>
        private async Task<double> NudgeCoordinateAsync(
            bool editingLatitude,
            double step,
            double latitude,
            double longitude)
        {
            string label = editingLatitude ? "Latitude" : "Longitude";

            while (true)
            {
                int choice = await ShowChoiceDialogAsync(
                    LocationSettingsModel.FormatCoordinates(latitude, longitude),
                    string.Format("{0} +/- {1}", label, LocationSettingsModel.FormatStep(step)),
                    string.Format("+ {0}", LocationSettingsModel.FormatStep(step)),
                    string.Format("- {0}", LocationSettingsModel.FormatStep(step)),
                    "Done");

                if (choice != 0 && choice != 1)
                {
                    return editingLatitude ? latitude : longitude;
                }

                double delta = choice == 0 ? step : -step;
                if (editingLatitude)
                {
                    latitude = LocationSettingsModel.AdjustLatitude(latitude, delta);
                }
                else
                {
                    longitude = LocationSettingsModel.AdjustLongitude(longitude, delta);
                }
            }
        }

        /// <summary>
        /// Shows a MessageDialog with up to three command buttons (the platform
        /// maximum) and returns the index of the chosen command, or -1 if the
        /// dialog was dismissed without a command.
        /// </summary>
        private static async Task<int> ShowChoiceDialogAsync(string content, string title, params string[] labels)
        {
            if (labels.Length > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(labels), "MessageDialog supports at most three commands.");
            }

            int selected = -1;
            var dialog = new MessageDialog(content, title);

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                dialog.Commands.Add(new UICommand(labels[i], cmd => { selected = index; }));
            }

            dialog.DefaultCommandIndex = 0;

            await dialog.ShowAsync();
            return selected;
        }

        private async void ShowAirplaneInfoDialog(AirplaneState plane)
        {
            if (_infoDialogShowing) return;
            _infoDialogShowing = true;

            try
            {
                float altM = plane.AltMeters;
                float altFt = altM * 3.28084f;
                float velKts = (plane.Velocity ?? 0f) * 1.94384f;

                var detail = string.Format(
                    "Callsign: {0}\n" +
                    "ICAO24: {1}\n" +
                    "Country: {2}\n" +
                    "Altitude: {3:F0} ft ({4:F0} m)\n" +
                    "Speed: {5:F0} kts\n" +
                    "Track: {6}\n" +
                    "On ground: {7}",
                    plane.DisplayName,
                    plane.Icao24,
                    plane.OriginCountry,
                    altFt, altM,
                    velKts,
                    plane.TrueTrack.HasValue
                        ? string.Format("{0:F0}°", plane.TrueTrack.Value)
                        : "N/A",
                    plane.OnGround ? "Yes" : "No");

                var dialog = new MessageDialog(detail, "Airplane Details");
                dialog.Commands.Add(new UICommand("Close", cmd => { }));
                dialog.DefaultCommandIndex = 0;
                dialog.CancelCommandIndex = 0;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AirplaneInfo] Dialog error: {ex.Message}");
            }
            finally
            {
                _infoDialogShowing = false;
            }
        }
    }
}
