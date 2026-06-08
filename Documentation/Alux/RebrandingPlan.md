# 리브랜딩 작업 계획서 — scratch-link → AluxLabs Link

원본 `scratch-link` (Scratch Foundation, AGPL-3.0) 의 Windows 포크를 **"AluxLabs Link"** 라는 독립 제품으로 분리하기 위한 전수 변경 목록.

## 1. 목표와 결정 사항

| 항목 | 결정값 | 비고 |
|---|---|---|
| 사용자 표시명 | **AluxLabs Link** | 트레이, 인스톨러, About 등 모든 UI |
| 회사명 | ALUX, Inc. | 이미 적용된 상태 |
| 상표 회피 | 제품명에 "Scratch" 단어 **사용 금지** | nominative fair use 도 안전 마진 위해 회피 |
| Scratch 호환성 표현 | 문서에 한정해 "Scratch 와 호환됨" 정도만 | "for Scratch" / "Scratch-compatible" 같이 endorsement 로 읽히는 표현 회피 |
| 라이선스 | AGPL-3.0-only 유지 | 원본과 동일. 파생 저작물 의무 준수 |
| 원본 attribution | 모든 파일의 `// Copyright (c) Scratch Foundation` 헤더 **보존** | AGPL §5 요구 |

### 1.1 식별자 매핑 (소스 of truth)

| 종류 | 기존 | 신규 |
|---|---|---|
| 사용자 표시명 (Display) | `Alux Scratch Link` / `scratch-link` | `AluxLabs Link` |
| 어셈블리명 (`AssemblyName`) | `Alux Scratch Link` | `AluxLabs Link` |
| 루트 네임스페이스 | `ScratchLink` | `AluxLabs.Link` |
| Windows 네임스페이스 | `ScratchLink.Win` | `AluxLabs.Link.Win` |
| 핵심 클래스 | `ScratchLinkApp` | `AluxLabsLinkApp` |
| 솔루션 파일 | `scratch-link.sln` | `aluxlabs-link.sln` |
| 폴더 (Windows 본체) | `scratch-link-win/` | `aluxlabs-link-win/` |
| 폴더 (공통 코드) | `scratch-link-common/` | `aluxlabs-link-common/` |
| 폴더 (MSIX) | `scratch-link-win-msix/` | `aluxlabs-link-win-msix/` |
| 프로젝트 파일 | `scratch-link-win.csproj` 등 | `aluxlabs-link-win.csproj` 등 |
| 아이콘 파일 | `scratch-link.ico` / `scratch-link-tray.ico` | `aluxlabs-link.ico` / `aluxlabs-link-tray.ico` |
| MSIX Package Identity `Name` | `ScratchFoundation.1711508CFD202` | `ALUXInc.AluxLabsLink` (임시. Partner Center 등록 시 발급값으로 교체) |
| MSIX Package Identity `Publisher` | `CN=2EC43DF1-469A-4119-9AB9-568A0A1FF65F` | 서명 인증서 Subject 의 CN (자체 서명 cert 또는 Store 발급값) |
| WebSocket 포트 | 20211 (변경 없음) | 동일 — Scratch 와 연결되는 endpoint |
| WebSocket path (`/scratch/ble` 등) | 변경 없음 | 동일 — Scratch 의 클라이언트 API 규약 |

## 2. 절대 변경 금지 (AGPL §5 / 상표 표시)

다음 항목은 라이선스/상표 의무 때문에 **건드리면 안 된다**.

| 항목 | 위치 | 이유 |
|---|---|---|
| AGPL 라이선스 전문 | `LICENSE` | AGPL §5 — 라이선스 텍스트 동봉 의무 |
| 상표 정책 문서 | `TRADEMARK` | Scratch Foundation 상표권 명시 — 삭제 시 법적 분쟁 위험 |
| 파일 헤더 `// <copyright file="X" company="Scratch Foundation">` | 모든 원본 유래 `.cs` | AGPL §5 — 원저작자 표시 보존 |
| `// Copyright (c) Scratch Foundation. All rights reserved.` | 모든 원본 유래 `.cs` | 동일 |
| `// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.` | ALUX 신규 작성 파일의 헤더 | 이미 정확한 attribution. 유지 |
| 프로토콜 식별자 문자열 | `/scratch/ble`, `/scratch/bt`, `/scratch/serial` (WebSocket path) | Scratch 클라이언트와의 wire-level 호환성 |
| 프로토콜 문서 | `Documentation/Architecture.md`, `Documentation/BluetoothLE.md`, `Documentation/Bluetooth.md`, `Documentation/NetworkProtocol.md`, `Documentation/TestPlans.md` | 원본 프로토콜 명세 — 사실상 historical reference. "Scratch Link" 단어를 protocol 명칭으로 보고 그대로 둠 |
| `Documentation/Alux/SerialApiReference.md` 등에서 protocol 을 가리키는 "Scratch Link" 언급 | 동일 docs 일부 | 같은 이유. 단 우리 제품을 가리키는 부분은 변경 |
| 코드 안 doc comment 의 "Scratch Link protocol" / "Scratch Link sessions" | `Session.cs:28,33`, `EncodingHelpers.cs:14,15`, `BLESession.cs:52`, `ScratchLinkApp.cs:13` 등 | 프로토콜의 일반명칭으로 해석. 변경 시 `Scratch Link 프로토콜` 등으로 명시화 가능 (선택) |

### 2.1 신규 추가 권장 (AGPL §5)

- **`NOTICE` 파일 신규 작성** — fork 출처와 변경자 명시. 본문 예시는 §7 참조
- 기존 원본 파일을 ALUX 가 **상당히 수정**한 경우 헤더 아래 한 줄 추가 (선택):
  `// Modified by ALUX, Inc. on 2026-MM-DD: <간단한 사유>`

## 3. 작업 범위 — 포함 / 제외

### 3.1 포함 (Windows 빌드 체인)

`scratch-link.sln` 에 포함되고 Windows 빌드에 영향:

- 루트 파일: `LICENSE`, `TRADEMARK`, `README.md`, `package.json`, `Makefile`, `playground.html`, `playground.js`, `.editorconfig`, `stylecop.json`, `release.config.js`, `renovate.json5`, `global.d.ts`
- `scratch-link-common/` (`.shproj`, `.projitems`, 모든 `.cs`)
- `scratch-link-win/` (`.csproj`, `.xaml`, 모든 `.cs`, `app.manifest`, 아이콘)
- `scratch-link-win-msix/` (`.wapproj`, `Package.appxmanifest`, 이미지)
- `SharedProps/` (`.props`, `.targets`)
- `Documentation/Alux/` 전체
- `.github/actions/windows-build/action.yml`
- `brand/build_icons.py`
- `CLAUDE.md`

### 3.2 제외 (이번 작업에서 손대지 않음)

| 항목 | 이유 |
|---|---|
| `scratch-link-mac/` 전체 | Mac 빌드 안 함. 솔루션에 "Unsupported" 로만 표시 |
| `Scratch Link Safari Helper/` 전체 | Mac Safari 확장. Windows 무관 |
| `scratch-link/` (MAUI 폴더) | 솔루션에서 빠진 레거시 코드. 별도 정리 권장 |
| `fastlane/` | Mac 서명 자동화 |
| `bin/`, `obj/`, `.vs/`, `*.user` | 자동 생성. 빌드 시 재생성 |
| `Scratch Link Safari Helper/` 안의 모든 파일명 | Mac 전용 |
| `.github/actions/macos-build/action.yml` | Mac CI |
| `Documentation/Architecture.md` 등 upstream 원본 문서 | §2 — 프로토콜 명세, AGPL attribution |

> Mac 프로젝트는 같은 `scratch-link-common` 을 import 하기 때문에, common 폴더/파일명을 바꾸면 `scratch-link-mac.csproj:212` 의 `Import Project="..\scratch-link-common\scratch-link-common.projitems"` 가 깨진다. Mac 은 어차피 안 빌드되므로 무시 가능하지만, 정리 차원에서 같이 업데이트하거나 솔루션에서 Mac 프로젝트를 빼는 것도 검토.

## 4. 작업 순서 (의존성 고려)

순서 잘못 잡으면 빌드가 깨지거나 식별자 충돌이 난다. 권장 순서:

| 단계 | 작업 | 빌드 영향 |
|---|---|---|
| 1 | MSIX Identity 임시값으로 변경 (cert 결정 전이라도 `ALUXInc.AluxLabsLink` + `CN=ALUX, Inc.` 자체 서명 임시 cert) | MSIX 빌드만 영향. EXE 빌드 무관 |
| 2 | UI 노출 문자열 변경 (DisplayName, ToolTip, About) | 빌드 영향 미미 |
| 3 | NOTICE 파일 신규 작성 + README 어트리뷰션 보강 | 빌드 무관 |
| 4 | `AssemblyName` / `RootNamespace` 변경 → `bin/obj` 전체 삭제 후 재빌드 | csproj 단위 |
| 5 | 모든 `.cs` 의 `namespace`, `using` 일괄 치환 (`ScratchLink` → `AluxLabs.Link`) | 빌드 깨짐 → 일관 치환 후 회복 |
| 6 | 클래스 `ScratchLinkApp` → `AluxLabsLinkApp` 리네임 | 빌드 깨짐 → 사용처 동시 치환 |
| 7 | XAML 의 `x:Class`, `xmlns:local` 갱신 | XAML 컴파일 |
| 8 | `.cs` 파일명 변경 (`ScratchLinkApp.cs` → `AluxLabsLinkApp.cs`) | 무영향 (csproj 가 와일드카드 include 면) |
| 9 | 아이콘 파일명 변경 + csproj 의 `Content Include` / `ApplicationIcon` 동시 갱신 | 빌드 |
| 10 | 프로젝트 파일명 변경 (`*.csproj`, `*.wapproj`, `*.shproj`, `.projitems`) + `.sln` 의 경로 동시 갱신 (ProjectGuid 보존) | 솔루션 로드 |
| 11 | 폴더명 변경 + `.sln` / csproj 의 모든 `..\scratch-link-*\` 경로 갱신 | 솔루션 |
| 12 | 솔루션 파일명 변경 (`scratch-link.sln` → `aluxlabs-link.sln`) | 마지막 |
| 13 | `Documentation/Alux/*.md`, `CLAUDE.md`, `README.md`, `.github/actions/windows-build/action.yml` 의 경로/이름 참조 갱신 | 문서 |
| 14 | 전체 빌드 검증 (Debug_Win / Release_Win 양쪽) | 최종 |

> **VS 의 리팩토링 기능 우선 활용**: 5~7 단계는 Visual Studio 의 "Rename" (F2) 가 가장 안전. namespace 변경 시 VS 가 `using` 까지 자동 갱신. sed 일괄 치환은 마지막 보루.

> **Git 커밋 분리 권장**: 단계별로 커밋. 한 커밋에 모두 몰면 충돌 시 분리 불가. 권장 단위: (a) MSIX identity 만, (b) UI 문자열 만, (c) AssemblyName/네임스페이스, (d) 파일/폴더 rename, (e) 문서 갱신.

## 5. 파일별 변경 목록

### 5.1 MSIX Identity (Tier A — 최우선)

#### [scratch-link-win-msix/Package.appxmanifest](../../scratch-link-win-msix/Package.appxmanifest)

| 라인 | 현재 | 변경 |
|---|---|---|
| 10 | `Name="ScratchFoundation.1711508CFD202"` | `Name="ALUXInc.AluxLabsLink"` (Store 발급 시 교체) |
| 11 | `Publisher="CN=2EC43DF1-469A-4119-9AB9-568A0A1FF65F"` | 서명 인증서 Subject CN 으로 교체 |
| 15 | `<DisplayName>Alux Scratch Link</DisplayName>` | `<DisplayName>AluxLabs Link</DisplayName>` |
| 16 | `<PublisherDisplayName>ALUX, Inc.</PublisherDisplayName>` | (그대로) |
| 34 | `DisplayName="Alux Scratch Link"` | `DisplayName="AluxLabs Link"` |
| 35 | `Description="Alux Scratch Link"` | `Description="AluxLabs Link"` |

#### [scratch-link-win/app.manifest](../../scratch-link-win/app.manifest)

| 라인 | 현재 | 변경 |
|---|---|---|
| 3 | `name="Alux Scratch Link.app"` | `name="AluxLabs Link.app"` |

### 5.2 UI 노출 문자열 (Tier B)

#### [scratch-link-win/TrayIcon.xaml](../../scratch-link-win/TrayIcon.xaml)

| 라인 | 현재 | 변경 |
|---|---|---|
| 7 | `xmlns:local="using:ScratchLink.Win"` | `xmlns:local="using:AluxLabs.Link.Win"` |
| 10 | `x:Key="ScratchLinkTaskbarIcon"` | `x:Key="AluxLabsLinkTaskbarIcon"` (App.xaml.cs:95 도 동시 갱신) |
| 12 | `ToolTipText="Alux Scratch Link"` | `ToolTipText="AluxLabs Link"` |
| 14 | `IconSource="scratch-link-tray.ico"` | `IconSource="aluxlabs-link-tray.ico"` |
| 26 | `Label="Alux Scratch Link 1.0.0.0"` | `Label="AluxLabs Link 1.0.0.0"` |

### 5.3 csproj / 어셈블리 메타데이터 (Tier B)

#### [scratch-link-win/scratch-link-win.csproj](../../scratch-link-win/scratch-link-win.csproj)

| 라인 | 현재 | 변경 |
|---|---|---|
| 7 | `<RootNamespace>ScratchLink.Win</RootNamespace>` | `<RootNamespace>AluxLabs.Link.Win</RootNamespace>` |
| 8 | `<AssemblyName>Alux Scratch Link</AssemblyName>` | `<AssemblyName>AluxLabs Link</AssemblyName>` |
| 21 | `<ApplicationIcon>scratch-link.ico</ApplicationIcon>` | `<ApplicationIcon>aluxlabs-link.ico</ApplicationIcon>` |
| 23 | `Import Project="..\scratch-link-common\scratch-link-common.projitems"` | 폴더/파일 리네임 후 `..\aluxlabs-link-common\aluxlabs-link-common.projitems` |
| 37 | `<Content Include="scratch-link.ico" />` | `<Content Include="aluxlabs-link.ico" />` |
| 38 | `<Content Include="scratch-link-tray.ico" />` | `<Content Include="aluxlabs-link-tray.ico" />` |

#### [scratch-link-win-msix/scratch-link-win-msix.wapproj](../../scratch-link-win-msix/scratch-link-win-msix.wapproj)

| 라인 | 현재 | 변경 |
|---|---|---|
| 51 | `<EntryPointProjectUniqueName>..\scratch-link-win\scratch-link-win.csproj</EntryPointProjectUniqueName>` | `..\aluxlabs-link-win\aluxlabs-link-win.csproj` |
| 99 | `<ProjectReference Include="..\scratch-link-win\scratch-link-win.csproj">` | `..\aluxlabs-link-win\aluxlabs-link-win.csproj` |

#### [SharedProps/ScratchVersion.targets](../../SharedProps/ScratchVersion.targets)

| 라인 | 현재 | 변경 |
|---|---|---|
| 47 | 코멘트: `"Alux Scratch Link 1.0.0.x"` | `"AluxLabs Link 1.0.0.x"` |

> 파일명 `ScratchVersion.targets` 자체는 MSBuild target name 이므로 굳이 안 바꿔도 됨. 바꾸려면 `AluxLabsVersion.targets` 로 변경하고 모든 `<Import Project="...ScratchVersion.targets"/>` 갱신.

### 5.4 C# 네임스페이스 일괄 치환 (Tier C — 가장 큰 작업)

**치환 규칙**:

| 기존 | 신규 |
|---|---|
| `namespace ScratchLink;` | `namespace AluxLabs.Link;` |
| `namespace ScratchLink.Win;` | `namespace AluxLabs.Link.Win;` |
| `namespace ScratchLink.Win.BLE;` | `namespace AluxLabs.Link.Win.BLE;` |
| `namespace ScratchLink.Win.BT;` | `namespace AluxLabs.Link.Win.BT;` |
| `namespace ScratchLink.Win.Serial;` | `namespace AluxLabs.Link.Win.Serial;` |
| `namespace ScratchLink.BLE;` | `namespace AluxLabs.Link.BLE;` |
| `namespace ScratchLink.BT;` | `namespace AluxLabs.Link.BT;` |
| `namespace ScratchLink.Serial;` | `namespace AluxLabs.Link.Serial;` |
| `namespace ScratchLink.JsonRpc;` | `namespace AluxLabs.Link.JsonRpc;` |
| `namespace ScratchLink.JsonRpc.Converters;` | `namespace AluxLabs.Link.JsonRpc.Converters;` |
| `namespace ScratchLink.Extensions;` | `namespace AluxLabs.Link.Extensions;` |
| `using ScratchLink;` / `using ScratchLink.*;` | 모두 대응되는 `AluxLabs.Link*` 로 |
| 클래스 `ScratchLinkApp` | `AluxLabsLinkApp` |

**영향 파일 목록** (전수, 약 50개 — `using` 만 갱신되는 케이스 포함):

`scratch-link-common/`:
- ScratchLinkApp.cs (파일명도 변경 → AluxLabsLinkApp.cs)
- Session.cs
- SessionManager.cs
- PeripheralSession.cs
- WebSocketListener.cs
- EncodingHelpers.cs
- EventAwaiter.cs
- BLE/IBLEEndpoint.cs
- BLE/BLESession.cs
- BLE/GattHelpers.cs
- BT/BTSession.cs
- Serial/SerialSession.cs
- Serial/SerialOpenParams.cs
- Serial/SerialDiscoveryFilter.cs
- JsonRpc/JsonRpc2Message.cs
- JsonRpc/JsonRpc2Request.cs
- JsonRpc/JsonRpc2Response.cs
- JsonRpc/JsonRpc2Error.cs
- JsonRpc/JsonRpc2Exception.cs
- JsonRpc/Converters/JsonRpc2MessageConverter.cs
- JsonRpc/Converters/JsonRpc2ValueConverter.cs
- Extensions/ContainerExtensions.cs
- Extensions/JsonExtensions.cs
- Extensions/SemaphoreSlimExtensions.cs

`scratch-link-win/`:
- App.xaml.cs (line 5, 11, 12, 27, 57, 95)
- App.xaml (line 5, 8 — `x:Class`, `xmlns:local`)
- TrayIcon.xaml (line 7 — `xmlns:local`)
- WinSessionManager.cs
- BLE/WinBLESession.cs
- BLE/WinBLEEndpoint.cs
- BLE/WinGattHelpers.cs
- BT/WinBTSession.cs
- Serial/WinSerialSession.cs
- Serial/WinSerialPortInfo.cs
- Serial/WinSerialPortEnumerator.cs

`scratch-link-common/scratch-link-common.projitems`:
- 라인 9: `<Import_RootNamespace>ScratchLink</Import_RootNamespace>` → `<Import_RootNamespace>AluxLabs.Link</Import_RootNamespace>`
- 라인 30: `<Compile Include="$(MSBuildThisFileDirectory)ScratchLinkApp.cs" />` → 파일명 변경 시 동시 갱신

### 5.5 파일명 변경 (Tier D)

| 기존 | 신규 |
|---|---|
| `scratch-link-common/ScratchLinkApp.cs` | `aluxlabs-link-common/AluxLabsLinkApp.cs` |
| `scratch-link-common/scratch-link-common.shproj` | `aluxlabs-link-common/aluxlabs-link-common.shproj` |
| `scratch-link-common/scratch-link-common.projitems` | `aluxlabs-link-common/aluxlabs-link-common.projitems` |
| `scratch-link-win/scratch-link-win.csproj` | `aluxlabs-link-win/aluxlabs-link-win.csproj` |
| `scratch-link-win/scratch-link.ico` | `aluxlabs-link-win/aluxlabs-link.ico` |
| `scratch-link-win/scratch-link-tray.ico` | `aluxlabs-link-win/aluxlabs-link-tray.ico` |
| `scratch-link-win-msix/scratch-link-win-msix.wapproj` | `aluxlabs-link-win-msix/aluxlabs-link-win-msix.wapproj` |
| `scratch-link.sln` | `aluxlabs-link.sln` |

### 5.6 폴더 변경 (Tier D)

| 기존 | 신규 |
|---|---|
| `scratch-link-common/` | `aluxlabs-link-common/` |
| `scratch-link-win/` | `aluxlabs-link-win/` |
| `scratch-link-win-msix/` | `aluxlabs-link-win-msix/` |

> 폴더 rename 시 `.sln` 의 모든 프로젝트 경로, 모든 csproj/projitems 의 `..\` 상대 경로, `.github/actions/windows-build/action.yml` 의 빌드 명령 경로 모두 동시 갱신 필요. ProjectGuid 는 **절대 바꾸지 말 것** — 솔루션이 GUID 로 프로젝트를 식별하기 때문.

### 5.7 .sln 파일 (Tier E)

#### [scratch-link.sln](../../scratch-link.sln)

| 라인 | 변경 |
|---|---|
| 33 | `"scratch-link-common", "scratch-link-common\scratch-link-common.shproj", ...` → `"aluxlabs-link-common", "aluxlabs-link-common\aluxlabs-link-common.shproj", ...` (GUID 유지) |
| 53 | `"scratch-link-win", "scratch-link-win\scratch-link-win.csproj", ...` → `"aluxlabs-link-win", "aluxlabs-link-win\aluxlabs-link-win.csproj", ...` (GUID 유지) |
| 55 | `"scratch-link-win-msix", "scratch-link-win-msix\scratch-link-win-msix.wapproj", ...` → 동일 패턴 |
| 223~225 | `scratch-link-common\scratch-link-common.projitems*{guid}*SharedItemsImports` 경로 갱신 |

> Mac 프로젝트 (`scratch-link-mac`, line 35) 는 그대로 두되, 안의 `Import Project="..\scratch-link-common\..."` 참조가 깨지므로 솔루션 빌드 시 unloaded 상태가 됨. Windows 빌드에는 영향 없음.

### 5.8 GitHub Actions (Tier F)

#### [.github/actions/windows-build/action.yml](../../.github/actions/windows-build/action.yml)

| 라인 | 현재 | 변경 |
|---|---|---|
| 22 | `msbuild scratch-link-win-msix/scratch-link-win-msix.wapproj ...` | `msbuild aluxlabs-link-win-msix/aluxlabs-link-win-msix.wapproj ...` |
| 28 | `mv -v scratch-link-win-msix/AppPackages/scratch-link-win-msix_*_${{...}}.msixupload Artifacts/` | 경로/패턴 갱신 |
| 30 | `for PKGPATH in scratch-link-win-msix/AppPackages/scratch-link-win-msix_*_..._Win_Test/scratch-link-win-msix_*_..._Win.msixbundle; do` | 동일 |
| 33 | 정규식 `scratch-link-win-msix_([.0-9]+)_(.*)_..._Win.msixbundle$` | `aluxlabs-link-win-msix_...` |
| 39, 41 | `mv -v "$PKGPATH" "Artifacts/Scratch Link ${PKGVERSION}.msixbundle"` | `"Artifacts/AluxLabs Link ${PKGVERSION}.msixbundle"` |

### 5.9 문서 / 메타데이터 (Tier F)

| 파일 | 변경 사항 |
|---|---|
| [README.md](../../README.md) | 제목 `# Alux Scratch Link` → `# AluxLabs Link`. 본문의 "Alux Scratch Link" 모든 인스턴스. fork 출처 attribution 강화 (§7 NOTICE 텍스트 참조). 저장소 구조 트리의 폴더명 갱신. |
| [CLAUDE.md](../../CLAUDE.md) | 폴더명/프로젝트명 언급 갱신. `scratch-link-common`, `scratch-link-win`, `scratch-link-win-msix` → 신규 이름 |
| [Documentation/Alux/WindowsDevSetup-VS2026.md](WindowsDevSetup-VS2026.md) | 라인 3, 7, 9~12, 32, 54~56, 66, 68, 76~78, 88~89, 94~95 — 폴더/파일/제품명 갱신 |
| [Documentation/Alux/SerialKeepAliveGuide.md](SerialKeepAliveGuide.md) | 라인 5 의 "Scratch Link serial transport" — protocol 표현은 유지하거나 "AluxLabs Link 의 serial transport (Scratch Link serial protocol 구현)" 식으로 명확화 |
| [Documentation/Alux/SerialApiReference.md](SerialApiReference.md) | 라인 358 의 "Scratch Link does not retry..." → "AluxLabs Link does not retry..." (구현 동작 설명이므로 우리 제품명으로) |
| [package.json](../../package.json) | `"name": "alux-scratch-link"` → `"name": "aluxlabs-link"`. description 갱신 |
| [Makefile](../../Makefile) | `scratch-link` 관련 타겟 경로 갱신 (Windows 빌드에 직접 관여하지는 않음 — icons 등) |
| [brand/build_icons.py](../../brand/build_icons.py) | 출력 파일명 `scratch-link.ico` / `scratch-link-tray.ico` → `aluxlabs-link.ico` / `aluxlabs-link-tray.ico` |
| `playground.html` / `playground.js` | 원본 테스트 페이지. "Scratch Link" 단어가 *우리 앱*을 가리키는 부분만 갱신. 프로토콜 설명 부분은 유지 |

## 6. 작업 후 검증 체크리스트

- [ ] `Documentation/Architecture.md`, `BluetoothLE.md`, `NetworkProtocol.md`, `TestPlans.md`, `Bluetooth.md` 의 원본 텍스트는 **변경되지 않았다**
- [ ] `LICENSE`, `TRADEMARK` 는 **변경되지 않았다**
- [ ] 모든 `.cs` 파일의 `// <copyright file="..." company="Scratch Foundation">` 헤더가 **유지되었다**
- [ ] `NOTICE` 파일이 새로 생성되었고 fork 출처가 명시되었다
- [ ] `README.md` 에 fork 출처 + AGPL 라이선스 표시 + 상표 무관 disclaimer 가 있다
- [ ] `grep -ri "Alux Scratch Link"` 결과 0건 (build 산출물 제외)
- [ ] `grep -ri "scratch-link"` 결과 — 원본 attribution 헤더와 프로토콜 명세 문서를 제외하면 0건
- [ ] `grep -ri "ScratchLink"` 결과 — 위와 동일 기준
- [ ] MSIX Package Identity 의 `Name`/`Publisher` 가 원본 ScratchFoundation 값이 **아니다**
- [ ] `dotnet build -c Debug_Win` 성공
- [ ] `msbuild aluxlabs-link-win-msix/aluxlabs-link-win-msix.wapproj -p:Configuration=Release_Win -p:Platform=x64` 성공
- [ ] 생성된 `.msixbundle` 파일명에 "AluxLabs Link" 가 들어가고 "Scratch" 가 들어가지 **않는다**
- [ ] MSIX 설치 후 시작 메뉴 / 트레이 / About 의 표시명이 모두 "AluxLabs Link" 다
- [ ] 트레이 아이콘 우클릭 → 버전 복사 후 클립보드에 "AluxLabs Link" 가 들어 있다
- [ ] 기존 PC 에 원본 Scratch Link 가 설치돼 있어도 충돌 없이 사이드바이사이드 설치된다
- [ ] WebSocket 포트 20211, path `/scratch/ble`, `/scratch/bt`, `/scratch/serial` 가 그대로 동작한다 (Scratch 호환성)

## 7. NOTICE 파일 신규 작성 (§2.1)

`NOTICE` 파일을 저장소 루트에 신규 생성. 권장 내용:

```
AluxLabs Link
Copyright (c) 2026 ALUX, Inc.

This product is derived from scratch-link by the Scratch Foundation
(https://github.com/scratchfoundation/scratch-link), originally licensed
under the GNU Affero General Public License v3.0 (AGPL-3.0-only).

This product is also distributed under the AGPL-3.0-only license.
See the LICENSE file for the full license text.

The following modifications have been made by ALUX, Inc.:
  - Removed macOS support and Safari Helper extension
  - Added USB Serial transport support
  - Changed default WebSocket port to 20211 to allow coexistence with
    the original Scratch Link on the same machine
  - Upgraded to .NET 8 and Windows App SDK 1.8
  - (etc.)

"Scratch" is a trademark of the Scratch Foundation. AluxLabs Link is
not affiliated with, endorsed by, or sponsored by the Scratch Foundation.
References to the "Scratch Link protocol" in source code documentation
refer to the network protocol established by the original scratch-link
project, used here for client compatibility.
```

README 상단에도 다음 블록을 추가 (이미 일부 표현 있음 — 통합/강화):

```markdown
This is a Windows-only fork of [scratch-link](https://github.com/scratchfoundation/scratch-link)
by the Scratch Foundation, redistributed under the AGPL-3.0-only license.

"Scratch" is a trademark of the Scratch Foundation. This product is not
affiliated with, endorsed by, or sponsored by the Scratch Foundation.
```

## 8. 리스크 / 함정

| 위험 | 대응 |
|---|---|
| ProjectGuid 변경으로 .sln 깨짐 | GUID 는 **유지**. 이름과 경로만 변경 |
| Mac 프로젝트 (`scratch-link-mac`) 의 `Import Project="..\scratch-link-common\..."` 경로 깨짐 | Windows 빌드 영향 없음. Mac 안 쓰면 무시 가능. 깔끔히 정리하려면 솔루션에서 Mac 프로젝트 제거 또는 동일 경로 갱신 |
| Visual Studio 캐시 (`.vs/`) 가 옛 경로 보존 | rename 후 `.vs/` 폴더 삭제 후 솔루션 재오픈 |
| `bin/obj` 의 옛 어셈블리명 산출물 잔존 | rename 후 두 폴더 모두 삭제 |
| MSIX 의 데이터 폴더 위치 변경 | `Identity Name` 이 바뀌면 `%LOCALAPPDATA%\Packages\` 경로가 새로 생김. 기존 데이터 마이그레이션 불필요 (개발 중인 fork 이므로) |
| GitHub Actions workflow 가 옛 경로로 실행되어 CI 실패 | `.github/actions/windows-build/action.yml` 의 경로/패턴 정규식 동시 갱신 후 push |
| 원본 Scratch Foundation 의 copyright header 를 실수로 제거 | grep 으로 사전 검증. `grep -rn "Copyright (c) Scratch Foundation"` 결과가 변경 전과 동일해야 함 |
| 프로토콜 doc comment 에서 "Scratch Link" 를 제품명인 줄 알고 변경 | §2 의 "프로토콜 식별자" 항목 참고. 모호하면 `Scratch Link protocol` 로 명시화 |
| Partner Center 등록 후 받은 Identity Name/Publisher 와 임시값 충돌 | Store 발급값을 받은 시점에 manifest 의 두 줄만 다시 교체. 다른 식별자 영향 없음 |

## 9. Out-of-scope — 별도 작업 검토

- `scratch-link/` MAUI 폴더 제거 여부 (현재 솔루션에 포함 안 되지만 디스크에 잔존)
- `scratch-link-mac/` 솔루션에서 분리 또는 별도 저장소로 옮기기
- `Scratch Link Safari Helper/` 폴더 통째 제거
- `fastlane/` 폴더 제거
- Documentation 폴더의 upstream 원본 문서들을 `Documentation/Upstream/` 같은 서브폴더로 정리 (구분 명확화)
