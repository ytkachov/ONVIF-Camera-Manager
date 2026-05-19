# ONVIF Camera Manager

A Windows desktop application for discovering and configuring IP cameras that support ONVIF. Built with WPF on .NET 8, with no third-party ONVIF libraries — camera communication is implemented directly over SOAP 1.2 / HTTP.

## Features

- **Network discovery** via WS-Discovery (UDP multicast on `239.255.255.250:3702`) with selectable local network interface.
- **Manual camera entry** by IP, port and credentials, with a connection probe.
- **Device information:** manufacturer, model, firmware version, serial number, hardware ID, ONVIF service endpoints and media profiles.
- **Video configuration:** edit encoder profiles (resolution, framerate, bitrate, GOP length, H.264 profile, rate-control type CBR/VBR/CQ), retrieve the RTSP stream URI.
- **Network configuration:** view and modify the camera's IPv4 interface settings (DHCP / static address, prefix length, MTU, DNS).
- **ONVIF authentication:** WS-Security `UsernameToken` with `PasswordDigest` (SHA-1, nonce + created) alongside HTTP Basic for broad camera compatibility.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
- Camera and host on the same L2 network for UDP discovery (multicast is not routed)

## Build and run

```powershell
dotnet build ONVIF.sln -c Release
dotnet run --project OnvifManager
```

Alternatively, open `ONVIF.sln` in Visual Studio 2022 (17.5+) or Rider and run the `OnvifManager` project.

## Usage

1. The sidebar opens on **Camera Details** by default. The top right pane is the discovery / camera list, the bottom pane shows parameters of the selected camera.
2. In the discovery pane, pick a network interface (or "Any") and click **Scan**, or enter IP / port / username / password to add a camera manually.
3. Select a camera in the list — its data loads into the bottom pane. The sidebar buttons **Device Info / Video Config / Network Config** switch what is shown there.
4. Changes in **Video Config** and **Network Config** are applied with the Save button.

## Architecture

```
OnvifManager/
├── App.xaml(.cs)           — DI composition root (Microsoft.Extensions.DependencyInjection)
├── MainWindow.xaml(.cs)    — shell with sidebar and split panel
├── Models/                 — POCOs: CameraDevice, CameraProfile, VideoEncoderConfig, NetworkInterfaceInfo, ImagingSettings, OnvifServiceUri
├── ViewModels/             — MVVM via CommunityToolkit.Mvvm (source generators)
│   ├── MainViewModel.cs         — navigation, aggregates child VMs
│   ├── DiscoveryViewModel.cs    — camera list, manual add, interface picker
│   ├── DeviceInfoViewModel.cs   — device info and services
│   ├── VideoConfigViewModel.cs  — encoder configurations and RTSP URI
│   └── NetworkConfigViewModel.cs— IPv4 / DNS / MTU
├── Views/                  — XAML for each VM (resolved through DataTemplate)
├── Services/
│   ├── DiscoveryService.cs      — WS-Discovery Probe/ProbeMatch over UdpClient
│   ├── OnvifClientFactory.cs    — OnvifClient (HttpClient + SOAP transport)
│   ├── DeviceService.cs         — GetDeviceInformation, GetServices, GetNetworkInterfaces, SetNetworkInterfaces
│   ├── MediaService.cs          — GetProfiles, GetVideoEncoderConfiguration(s), SetVideoEncoderConfiguration, GetStreamUri
│   ├── ImagingService.cs        — GetImagingSettings, SetImagingSettings
│   ├── SoapMessageBuilder.cs    — SOAP 1.2 envelope + WS-Addressing assembly
│   ├── SoapMessageParser.cs     — body parsing
│   ├── WsSecurityHelper.cs      — UsernameToken / PasswordDigest (SHA-1)
│   └── OnvifXml.cs              — namespace constants and message templates
└── Converters/             — WPF value converters
```

**Data flow:** `DiscoveryViewModel` owns the selected camera and raises a `CameraSelected` event. The other VMs subscribe to it and, when the selection changes, independently load their data through the appropriate ONVIF service, reusing the same `CameraDevice` (with credentials).

## Tech stack

- **.NET 8** / C# 12, nullable enabled
- **WPF** (target `net8.0-windows`)
- **CommunityToolkit.Mvvm 8.2.2** — `[ObservableProperty]`, `[RelayCommand]`
- **Microsoft.Extensions.DependencyInjection 8.0.1**
- No third-party ONVIF wrappers, no WSDL-generated proxies, no WCF — all SOAP is hand-rolled with `System.Xml.Linq` and `HttpClient`.

## Limitations

- IPv4 only in the network configuration.
- PTZ, events, analytics and audio are not implemented.
- No in-app video preview: the application surfaces the RTSP URI; play it back in an external player (VLC, etc.).
- Camera credentials are persisted to `%APPDATA%/SeaGull/cameras.json`, encrypted with DPAPI under the current Windows user. They cannot be decrypted by a different user or on a different machine.
- The camera's TLS certificate is not validated (`ServerCertificateCustomValidationCallback` accepts any) — be aware of this outside a trusted network.

## Known issues

- **Hikvision NVR friendly name is not auto-populated.** On Hikvision NVR firmware (observed on `DS-I200(D)` running `V5.5.120`) the ISAPI `GET /ISAPI/System/deviceInfo` call used to read the device name returns `401 Unauthorized` even after a Digest re-authentication round-trip. The same code works on Hikvision IP cameras and the *write* path (renaming the camera from the Device Info tab) works on the same NVRs. As a workaround, the camera shows the auto-name `"{Manufacturer} {Model}"` until you rename it manually — the new name persists across restarts.

## License

TBD.
