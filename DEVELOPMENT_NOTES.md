# Development Notes & Hardware Constraints

## Dependency Policy: SharpDX
**Do NOT upgrade SharpDX dependencies beyond version 3.0.2.**

### Reasoning
The project targets the **Microsoft HoloLens 1st Gen**, which operates on an x86 Intel Atom CPU architecture and relies on the **.NET Native AOT compilation** pipeline.

*   **Version 3.0.2:** Verified stable and compatible with the .NET Native toolchain on the HoloLens 1 Windows Holographic OS.
*   **Version 4.x:** While functionally similar on desktop, the 4.x series (the final release branch) introduced Marshalling and Interop changes that cause severe issues during .NET Native AOT compilation for x86. Common symptoms include:
    *   `TypeLoadException` at runtime.
    *   Namespace conflicts between `SharpDX.Mathematics` and `System.Numerics`.
    *   Silent crashes in `Release` builds on the physical HoloLens 1 device.

Any attempt to upgrade `SharpDX` beyond 3.0.2 will likely break the production build. Ensure Renovate or future manual updates do not increment these packages.

## CI/CD Pipeline Constraints: Runner Images
**Do NOT upgrade the GitHub Actions runner image from `windows-2022` to `windows-2025` (or later).**

### Reasoning
The HoloLens 1 application is built using the **Universal Windows Platform (UWP)**, specifically targeting Windows SDK `10.0.19041.0`.

*   **Toolchain Stability:** Legacy UWP projects are strictly bound to the MSBuild, Visual Studio, and Windows SDK toolsets present on the runner.
*   **Breaking Changes:** Newer runner images (e.g., `windows-2025`) update the default Visual Studio and MSBuild versions, which frequently deprecate or remove support for legacy UWP components and older Windows SDKs required to generate valid HoloLens 1 app packages.
*   **Build Reliability:** Upgrading runners has historically caused failures in the `vs_installer.exe` scripts used to dynamically install required older SDKs, as well as breaking changes in the .NET Native AOT toolchain, leading to the `WMC9999` internal XAML compiler errors observed when trying to modernize the pipeline.

Maintain build stability by pinning the runner image to `windows-2022`.
