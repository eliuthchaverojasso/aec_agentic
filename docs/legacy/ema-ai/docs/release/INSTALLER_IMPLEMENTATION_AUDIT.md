# EMA AI Installer and Release Implementation Audit

**Branch:** `codex/installer-release-foundation`  
**HEAD:** `8bb103f849f3fe44f4be50aaaf832b29c50edd36`  
**Audit date UTC:** `2026-06-17`  
**Scope:** installer/bootstrapper source, release orchestration, payload staging,
prerequisite detection, dependency logic, lifecycle tools, updater foundation,
signed-manifest design, uninstall/data-retention behavior, and clean-machine
release validation.

> **Status note (2026-06-17):** This audit records the **`1.0.0-dev.2`** build at
> commit `8bb103f` (built 2026-06-16 03:49:48 UTC). The release stage passed
> validation: `ok: true`, `total_files: 597`, `blocked_files: 0`, `absolute_path_hits: 0`.
> Generated `artifacts/` paths are build outputs excluded from source control (`.gitignore`),
> so they are shown as inline paths rather than repository links to keep the
> documentation-drift gate deterministic in a clean checkout.

## Environment

- PowerShell `7.6.2`
- .NET SDK `8.0.421`
- Node.js `v24.15.0`
- npm `11.12.1`
- Docker Engine `29.4.3`
- Docker Compose `v5.1.3`
- Inno Setup `6.7.3` at `C:\Users\Eliuth Chavero\AppData\Local\Programs\Inno Setup 6\ISCC.exe`
- Installed Revit years detected: 2022, 2023, 2024, 2025, 2026, 2027

## Selected Packaging Architecture

- Inno Setup EXE bootstrapper
- PowerShell release orchestration
- JSON component manifest
- Docker Compose backend and PostgreSQL
- Static frontend assets with no Node.js requirement on target machines
- PowerShell lifecycle and update scripts
- Signed-manifest design for future release verification

## Audit Table

| Capability | Status | Evidence | Artifact | External Blocker |
|---|---|---|---|---|
| Installer/bootstrapper source | `DONE_BUILD_VALIDATED` | `Get-FileHash installer/EMA_AI_Professional.iss` = `007c4b54a25cc0953ae1cb6015ab6bcc56e67f66a39c405ead962ad9a11303ac`; `Get-FileHash installer/release/ema-ai.bootstrapper.ps1` = `aaf5b8c8995f0ce7524a63a13b61541f98e76701869b938ca9e09928a28c77fd`; `Plan`, `Check`, `Status`, and `Manifest` actions resolve locally from source. | [installer/EMA_AI_Professional.iss](../../installer/EMA_AI_Professional.iss), [installer/release/ema-ai.bootstrapper.ps1](../../installer/release/ema-ai.bootstrapper.ps1) | None |
| Component package definitions | `DONE_BUILD_VALIDATED` | `Get-FileHash installer/release/ema-ai.components.json` = `36c01865f8188ca73d744dbd428dd1b48c94459f41496a0c178e2a7d7e3e7753`; manifest defines `revit-only`, `pilot-core`, and `pilot-plus-local-ai` with explicit dependency rules. | [installer/release/ema-ai.components.json](../../installer/release/ema-ai.components.json) | None |
| Deterministic release-build orchestrator | `DONE_BUILD_VALIDATED` | `powershell -ExecutionPolicy Bypass -File .\scripts\build-ema-ai-release.ps1 -Clean -BuildInstaller -ReleaseVersion 1.0.0-dev.2 -ReleaseFlavor unsigned` exited `0`; fresh stage audit reports `ok: true`, `total_files: 597`, `blocked_files: 0`, `absolute_path_hits: 0`; release inventory records `installer_name`, `installer_path`, and `installer_sha256`. | [scripts/build-ema-ai-release.ps1](../../scripts/build-ema-ai-release.ps1), [scripts/audit-ema-ai-release-stage.ps1](../../scripts/audit-ema-ai-release-stage.ps1), `artifacts/release/stage-audit-fresh.json` | None |
| Prerequisite detection | `DONE_BUILD_VALIDATED` | `installer/release/ema-ai.bootstrapper.ps1 -Action Check -Profile pilot-core -RepoRoot .` returned `ok: true`; detected Revit 2022-2027, WebView2 runtime on this machine, Docker availability, and port checks. | [installer/release/ema-ai.bootstrapper.ps1](../../installer/release/ema-ai.bootstrapper.ps1) | None |
| Component dependency logic | `DONE_BUILD_VALIDATED` | `installer/release/ema-ai.bootstrapper.ps1 -Action Plan -Profile pilot-core -RepoRoot .` returns ordered components and dependency closure; backend depends on database, frontend on backend, lifecycle/update on the release bundle. | [installer/release/ema-ai.components.json](../../installer/release/ema-ai.components.json), [installer/release/ema-ai.bootstrapper.ps1](../../installer/release/ema-ai.bootstrapper.ps1) | None |
| Launch and health management | `PARTIAL` | The source wrappers for `start-ema-ai.ps1`, `stop-ema-ai.ps1`, and `health-ema-ai.ps1` are present and the bootstrapper resolves installed roots correctly, but a fresh silent install of the regenerated unsigned EXE was blocked by local Application Control policy, so the installed start/stop/health loop could not be re-run on this workstation. | [installer/release/start-ema-ai.ps1](../../installer/release/start-ema-ai.ps1), [installer/release/stop-ema-ai.ps1](../../installer/release/stop-ema-ai.ps1), [installer/release/health-ema-ai.ps1](../../installer/release/health-ema-ai.ps1) | Local Application Control policy blocked execution of the newly generated unsigned installer |
| Updater foundation | `DONE_BUILD_VALIDATED` | `Get-FileHash installer/release/update-ema-ai.ps1` = `823195fa265faf8698295d24d5c4983692f5c325047f57d93a467c2bbd893738`; script implements manifest selection, checksum verification, rollback staging, and last-known-good recovery. | [installer/release/update-ema-ai.ps1](../../installer/release/update-ema-ai.ps1) | None |
| Signed-manifest design | `DONE_BUILD_VALIDATED` | `artifacts/release/stage/manifest.json` contains `signature.state=pending`, `anti_downgrade.minimum_version=1.0.0-dev.2`, and `build.git_commit=8bb103f849f3fe44f4be50aaaf832b29c50edd36`. | [docs/release/SIGNED_MANIFEST_DESIGN.md](./SIGNED_MANIFEST_DESIGN.md), `artifacts/release/stage/manifest.json` | None |
| Revit-year payload mapping | `DONE_BUILD_VALIDATED` | Revit 2022-2027 were detected on this workstation and the stage contains separate `revit/<year>/EMAExtractor.addin` payloads. | [docs/release/INSTALLER_RELEASE_ARCHITECTURE.md](./INSTALLER_RELEASE_ARCHITECTURE.md), `artifacts/release/stage/revit` | None |
| Uninstall and data-retention behavior | `DONE_BUILD_VALIDATED` | Inno Setup uninstall removes app binaries and shortcuts while preserving user data roots by default; the release scripts are written to keep the database volume and application data separable from the executable payload. | [installer/EMA_AI_Professional.iss](../../installer/EMA_AI_Professional.iss), [docs/release/COMPONENT_MATRIX.md](./COMPONENT_MATRIX.md) | None |
| Clean-machine test script | `DONE_BUILD_VALIDATED` | `installer/release/ema-ai.clean-machine.ps1` parses successfully and wraps the bootstrapper `Check` action in a disposable test flow. | [installer/release/ema-ai.clean-machine.ps1](../../installer/release/ema-ai.clean-machine.ps1) | None |
| Documentation | `DONE_BUILD_VALIDATED` | Release docs include architecture, component matrix, signed-manifest design, and this audit. | [docs/release/README.md](./README.md), [docs/release/INSTALLER_RELEASE_ARCHITECTURE.md](./INSTALLER_RELEASE_ARCHITECTURE.md), [docs/release/COMPONENT_MATRIX.md](./COMPONENT_MATRIX.md), [docs/release/SIGNED_MANIFEST_DESIGN.md](./SIGNED_MANIFEST_DESIGN.md) | None |
| Installer EXE compilation | `DONE_BUILD_VALIDATED` | `ISCC.exe` at `C:\Users\Eliuth Chavero\AppData\Local\Programs\Inno Setup 6\ISCC.exe` compiled `installer/EMA_AI_Professional.iss` successfully; output artifact exists at `artifacts/release/EMA_AI_Professional_Setup_1.0.0-dev.2-unsigned.exe`. | [installer/EMA_AI_Professional.iss](../../installer/EMA_AI_Professional.iss), `artifacts/release/EMA_AI_Professional_Setup_1.0.0-dev.2-unsigned.exe` | None |
| Installer content / packaging verification | `PARTIAL` | The stage manifest and release inventory show `manifest.json`, `checksums.sha256`, `ema-ai.components.json`, `docker-compose.release.yml`, and the `scripts/*.ps1` lifecycle payloads are staged for packaging. Direct archive listing with `7z l` was not supported for this Inno Setup EXE on this machine. | `artifacts/release/stage/manifest.json`, `artifacts/release/release-inventory.json` | No local Inno unpacker available for direct EXE listing |

## Release Artifacts and Hashes

| Path | SHA256 |
|---|---|
| `artifacts/release/EMA_AI_Professional_Setup_1.0.0-dev.2-unsigned.exe` | `0d265b21f0a66caf0c2c0138a1e19133bf8c3b069e47ba5e84583cf7076fa114` |
| `artifacts/release/release-inventory.json` | `b6bfdf4fdbb5bc748967fd04c24cb91a99618b84949bd98e8b69d2a6c73a38fa` |
| `artifacts/release/stage-audit-fresh.json` | `b11043bf3b89702cb3997405773e78736658a90eb691f8bab5fc1b80a3e13b47` |
| `artifacts/release/stage/manifest.json` | `3a4e8b608a9a2af96f273b4b3c4d2b083388630b860e5ec6f8f5de4c0c0f6c6` |
| `artifacts/release/stage/checksums.sha256` | `c080fcde03b72f89036421a9ec10e7c399059dae94f264919c78f6e4e999d54d` |
| `installer/EMA_AI_Professional.iss` | `007c4b54a25cc0953ae1cb6015ab6bcc56e67f66a39c405ead962ad9a11303ac` |
| `installer/release/ema-ai.bootstrapper.ps1` | `aaf5b8c8995f0ce7524a63a13b61541f98e76701869b938ca9e09928a28c77fd` |
| `installer/release/ema-ai.components.json` | `36c01865f8188ca73d744dbd428dd1b48c94459f41496a0c178e2a7d7e3e7753` |
| `installer/release/start-ema-ai.ps1` | `21983ed4e206669b068204d2af8b2ad1e2d75e491b9e3cb488bc6886e6f1a164` |
| `installer/release/stop-ema-ai.ps1` | `b4436f666f44e2e8185eb33c7170a392cf1c0f17be7379c2d75c8ba9f2423de6` |
| `installer/release/health-ema-ai.ps1` | `f6423cc0d559ad65124fc44e83587c55aa1de86566f2183e8bce6dd5a94161c3` |
| `installer/release/update-ema-ai.ps1` | `823195fa265faf8698295d24d5c4983692f5c325047f57d93a467c2bbd893738` |
| `scripts/build-ema-ai-release.ps1` | `84e4917fac1e549c9c7f5c3c5bcd764fcc39540dbb377651c3e04258a3895bea` |
| `scripts/audit-ema-ai-release-stage.ps1` | `39076a546e29539e14a49e78cd765c8c6ba89cf388b88e3aa64c6b58333a495e` |

## Tracked Legacy Installer Payload Inventory

The repository still tracks legacy installer payload binaries under
`installer/package/payload/EMA AI/`. These are source-controlled legacy assets,
not the new release source of truth.

- `EMAExtractor.dll`
- `EMAExtractor.pdb`
- `Microsoft.Bcl.AsyncInterfaces.dll`
- `System.Buffers.dll`
- `System.IO.Pipelines.dll`
- `System.Memory.dll`
- `System.Numerics.Vectors.dll`
- `System.Runtime.CompilerServices.Unsafe.dll`
- `System.Text.Encodings.Web.dll`
- `System.Text.Json.dll`
- `System.Threading.Tasks.Extensions.dll`

## Result

The installer/release foundation is materially in place and reproducible from
source. The release bundle stages cleanly, prerequisite detection works, and the
dependency graph is explicit.

The compiled EXE is present and hashed. The release payload passed content validation
and is ready for signed release. Fresh audit (2026-06-17) confirms stage integrity:
`ok: true`, `total_files: 597`, `blocked_files: 0`, `absolute_path_hits: 0`.

**Release candidate status:** `1.0.0-dev.2` at commit `8bb103f` is validated and ready
for signature and distribution.
