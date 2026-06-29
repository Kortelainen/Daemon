# EbOverlay — Release Process

Releases are published manually as standalone Windows executables on GitHub Releases.
No installer — the user downloads a single `.exe` and runs it.

---

## Prerequisites

- .NET 8 SDK installed (`dotnet --version` → 8.x)
- Working directory: `C:\Kortelainen\EbOverlay`

---

## 1. Bump the version

Edit `src/EbOverlay/EbOverlay/EbOverlay.csproj`:

```xml
<Version>1.0.1</Version>
```

Use semantic versioning: `MAJOR.MINOR.PATCH`
- **PATCH** — bug fixes, threshold tweaks, visual adjustments
- **MINOR** — new features (new milestone completed)
- **MAJOR** — breaking changes or major redesigns

Commit the version bump before publishing:

```
git add src/EbOverlay/EbOverlay/EbOverlay.csproj
git commit -m "version X.Y.Z"
git tag vX.Y.Z
git push && git push --tags
```

---

## 2. Build the release exe

```powershell
cd src\EbOverlay\EbOverlay
dotnet restore -r win-x64
dotnet publish -p:PublishProfile=Release-x64
```

Output: `C:\Kortelainen\EbOverlay\publish\EbOverlay.exe`

The exe is self-contained — no .NET runtime or DLLs needed on the target machine.
The spritesheet is embedded inside the exe.

> **First-run note:** LibreHardwareMonitor extracts its hardware sensor driver
> (`WinRing0x64.sys`) to `%TEMP%` on first launch. This is normal and expected.

---

## 3. Test the release build locally

Run the published exe directly (not from Visual Studio):

```powershell
.\publish\EbOverlay.exe
```

Check:
- [ ] Overlay appears with sprite and metrics
- [ ] Tray icon visible, right-click menu works
- [ ] "Test status icons" option is **not** present (debug-only)
- [ ] No `sensor_dump.txt` created in the EbOverlay folder (debug-only)
- [ ] Sprite animates through states
- [ ] System metrics update every ~2 seconds
- [ ] UAC elevation prompt appears (required for hardware sensors)

---

## 4. Publish on GitHub Releases

1. Go to the repository on GitHub → **Releases** → **Draft a new release**
2. Choose the tag you pushed: `vX.Y.Z`
3. Release title: `EbOverlay vX.Y.Z`
4. Upload `publish\EbOverlay.exe` as the release asset
5. Write release notes (see template below)
6. Publish

### Release notes template

```markdown
## EbOverlay vX.Y.Z

### What's new
- ...

### Requirements
- Windows 10/11 x64
- Run as Administrator (required for CPU/GPU temperature sensors)

### Installation
Download `EbOverlay.exe` and run it. No installer needed.

> Windows Defender / SmartScreen may warn on first run because the exe is unsigned.
> Click "More info → Run anyway" to proceed.
> (Signing with a certificate is planned for a future release.)
```

---

## Notes

### Windows Defender / antivirus
The exe bundles the .NET runtime and requires admin elevation, which can trigger
false positives. This is expected until the exe is signed with a code-signing
certificate. Users need to allow it manually or add an exclusion.

### Code signing (future)
Once a certificate is obtained:
```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /f certificate.pfx /p <password> publish\EbOverlay.exe
```

### Debug vs Release differences
| Feature              | Debug | Release |
|----------------------|-------|---------|
| Sensor dump to file  | ✅    | ❌      |
| "Test status icons"  | ✅    | ❌      |
| Single-file exe      | ❌    | ✅      |
| Optimised IL         | ❌    | ✅      |

### Replacing the spritesheet
The spritesheet is embedded as a compiled resource. To update it:
1. Replace `src/EbOverlay/EbOverlay/Sprites/spritesheet.png`
2. Rebuild and republish — no other changes needed
3. Sheet dimensions must remain 768×1248 px (13 rows × 8 cols × 96×96 px frames)
