# faceBukDL

A .NET MAUI app that checks for a copied Facebook video link when opened or resumed, then provides a place to resolve and save an authorized video stream.

> **Resolver status:** The app does not currently extract video streams from Facebook pages. `DirectVideoUrlResolver` intentionally reports this limitation. A Facebook share URL is not a direct MP4 URL; configure a resolver for a source you are authorized to access before expecting automatic downloads to complete.

## Features

- Checks the clipboard at initial page display and when the app resumes.
- Recognizes `facebook.com` and `fb.watch` links.
- Shows download progress and supports cancellation.
- Streams files to app-private storage with a 500 MB size cap and `.partial` cleanup on failure.
- Restricts the stream URL to HTTPS and an explicitly configured host allowlist.
- Lets the user share/export a completed video through the platform share sheet.
- Persists completed downloads in a local JSON library with open, share, and delete actions.
- Uses an MVVM-style view model and injectable clipboard, export, resolver, and download services.

## Requirements

- .NET 10 SDK with the .NET MAUI workload installed.
- Android SDK for Android builds.
- A Mac with Xcode for iOS and Mac Catalyst builds/signing.

The project conditionally targets Android, iOS, Mac Catalyst, and Windows depending on the host OS. The minimum configured platform versions are Android 21, iOS 15, Mac Catalyst 17, and Windows 10 build 17763.

## Build

From the repository root:

```powershell
dotnet restore .\faceBukDL\faceBukDL.csproj
dotnet build .\faceBukDL\faceBukDL.csproj -f net10.0-android
```

On a Mac with Xcode:

```bash
dotnet build faceBukDL/faceBukDL.csproj -f net10.0-ios
```

Run on an Android device/emulator with the appropriate MAUI workload and device configured:

```powershell
dotnet build .\faceBukDL\faceBukDL.csproj -t:Run -f net10.0-android
```

## How it works

### Clipboard and lifecycle

`App.xaml.cs` subscribes to `Window.Resumed`; `MainPage` checks once in `OnAppearing`. Both route to `MainViewModel.CheckClipboardAsync()`. `IClipboardService` wraps the MAUI clipboard API so clipboard behavior can be replaced or mocked independently.

On iOS, reading the pasteboard may show a system privacy prompt. The app includes a manual **Check copied link** action as a user-visible fallback. Clipboard reads require the app to be foregrounded; the app does not monitor clipboard contents in the background.

### Download pipeline

`IVideoDownloadService` asks `IVideoUrlResolver` for a direct stream URI. The downloader then:

1. Requires HTTPS and a host in its configured allowlist.
2. Requests response headers first and validates video content type.
3. Enforces a 500 MB maximum, including when the response omits `Content-Length`.
4. Streams to `FileSystem.AppDataDirectory` using a temporary `.partial` file.
5. Renames the temporary file only after a successful, uncancelled download.

To configure a resolver, implement `IVideoUrlResolver` in `Services/VideoDownloadService.cs`, register it in `MauiProgram.cs`, and add only its known stream hosts to `VideoDownloadService.AllowedHosts`. Do not log signed stream URLs, cookies, or access tokens. Do not use scraping or third-party services unless you have verified authorization, privacy, and applicable platform terms.

### Export/storage

Completed files live in the app-private data directory and are not automatically added to Photos or a public Downloads folder. The **Share or export saved video** action opens the platform share sheet; the user chooses an available destination. No broad storage permission is needed for this private storage flow.

### Platform permissions

Android declares `INTERNET` and `ACCESS_NETWORK_STATE` in `Platforms/Android/AndroidManifest.xml`. It does not declare a clipboard permission (there is no general `READ_CLIPBOARD` permission) or broad storage access. iOS needs no Info.plist permission key for app-private storage or pasteboard reads; the OS controls paste privacy behavior.

## Project layout

```text
faceBukDL/
  MainPage.xaml                  Downloader home screen
  MainPage.xaml.cs               Page lifecycle hookup
  ViewModels/MainViewModel.cs    Clipboard, progress, and command state
  Services/PlatformServices.cs   Clipboard and share-sheet adapters
  Services/VideoDownloadService.cs
                                 URL validation, resolver contract, streaming, storage
  Platforms/Android/             Android manifest and entry points
  Platforms/iOS/                 iOS configuration and entry points
```

## Current limitations

- The default resolver intentionally cannot turn Facebook page/share links into MP4 stream URLs. Consequently, end-to-end Facebook downloading is not functional until an authorized resolver is supplied.
- The host allowlist starts empty. Add the trusted hosts returned by the configured resolver before downloads can pass validation.
- Downloads are foreground `HttpClient` operations. They can be interrupted if the OS suspends or terminates the app; use platform background transfer APIs if background completion is a product requirement.
- Downloads use a foreground `HttpClient`; interrupted transfers are cleaned up and must be restarted. Native background transfer support is not implemented yet.
- The library stores metadata in app-private JSON and removes entries when the user deletes a video; there is no automatic age-based cleanup policy yet.
