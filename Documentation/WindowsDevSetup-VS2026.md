# Windows 개발 환경 세팅 (Visual Studio 2026)

이 문서는 **Visual Studio 2026** 으로 Scratch Link 윈도우 버전을 빌드/디버깅하기 위한 환경 세팅 절차를 정리한 것이다. Visual Studio 2022 와는 워크로드 이름과 일부 컴포넌트 구성이 달라서 별도 가이드가 필요하다.

## 0. 사전 정보

- 솔루션 파일 `scratch-link.sln` 은 VS 2022 (v17) 포맷이지만 VS 2026 에서 그대로 열 수 있다. **버전 변환 프롬프트가 떠도 변환하지 말 것** (sln 포맷이 바뀌어 PR 이 지저분해진다).
- 윈도우 버전 프로젝트:
  - `scratch-link-win` — WinUI 3 기반 본체 EXE
  - `scratch-link-win-msix` — `.wapproj` (Desktop Bridge) 형식의 MSIX 패키징 프로젝트
  - `scratch-link-common` — 공유 C# 코드 (`.shproj`)
- 맥용 `scratch-link-mac` 은 솔루션을 열면 "Unsupported" 로 표시되는데 **정상이다**. 윈도우 VS 에서는 어차피 빌드하지 않으므로 무시한다 (솔루션에서 제거하지 말 것 — `.sln` 이 수정되어 git diff 에 잡힌다).

## 1. Visual Studio Installer 워크로드

VS Installer 를 열고 **수정(Modify)** 으로 다음 워크로드를 체크한다.

### 워크로드 (Workloads 탭)

- ☑ **.NET 데스크톱 개발** (.NET desktop development)
- ☑ **C++를 사용한 데스크톱 개발** (Desktop development with C++)
- ☑ **WinUI 애플리케이션 개발** (WinUI application development)
  - VS 2022 의 "Windows 응용 프로그램 개발" 워크로드가 VS 2026 에서 이 이름으로 바뀌었다.

### 각 워크로드의 선택 사항

워크로드를 체크한 뒤 우측 "설치 세부 정보" 패널에서 추가로 다음 항목을 켠다.

**`.NET 데스크톱 개발` 의 선택 사항:**

- ☑ **MSIX Packaging Tools** — `.wapproj` 빌드에 필수. VS 2026 에서는 개별 구성 요소 검색에 안 나오고 이 워크로드 안에 들어 있다.

**`WinUI 애플리케이션 개발` 의 선택 사항:**

- ☑ **Windows 11 SDK (10.0.22621.0)** — `scratch-link-win.csproj` 의 `TargetFramework=net6.0-windows10.0.22621.0` 가 요구하는 SDK.

`유니버설 Windows 플랫폼 도구` 는 이 프로젝트에 필요 없다.

## 2. .NET 6 SDK 별도 설치

VS 2026 인스톨러에는 **.NET 6 런타임만 포함되어 있고 SDK 는 빠져 있다** (.NET 6 은 2024-11 EOL). 프로젝트가 `net6.0-windows10.0.22621.0` 을 타겟팅하므로 SDK 를 따로 받아야 한다.

1. <https://dotnet.microsoft.com/download/dotnet/6.0> 접속
2. 표에서 **Windows 행 → 설치 관리자(Installer) 열 → `x64`** 클릭
   - `전체 (dotnet-install scripts)` 는 CI/스크립트용이므로 선택하지 말 것
   - `바이너리(Binaries)` 도 압축본이므로 일반 설치엔 부적합
3. 다운받은 `dotnet-sdk-6.0.xxx-win-x64.exe` 실행
4. 설치 후 **새 PowerShell** 을 열어 확인:

   ```powershell
   dotnet --list-sdks
   ```

   `6.0.xxx [C:\Program Files\dotnet\sdk]` 가 보이면 OK.

## 3. Windows App Runtime 1.3 설치

이 프로젝트는 `Microsoft.WindowsAppSDK 1.3.230331000` 을 framework-dependent 모드로 참조한다 (`SharedProps/WindowsSDK.props`, `scratch-link-win.csproj` 의 `<WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>`). 따라서 **Windows App Runtime 1.3 이 시스템에 설치되어 있어야** 디버그 실행이 된다.

설치 안 된 상태에서 F5 를 누르면 다음 다이얼로그가 뜬다:

> This application requires the Windows App Runtime Version 1.3 (MSIX package version >= 3000.820.152.0). Do you want to install a compatible Windows App Runtime now?

다이얼로그에서 **예(Y)** 를 누르면 Microsoft 사이트로 안내된다. 자동 안내가 실패할 경우 수동 설치:

1. <https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads-archive> 접속
2. **"Windows App SDK 1.3"** 섹션을 찾아 **`WindowsAppRuntimeInstall-x64.exe`** 다운로드 (ARM PC 라면 `-arm64.exe`)
3. 실행하여 설치
4. 설치 확인:

   ```powershell
   Get-AppxPackage -Name "Microsoft.WindowsAppRuntime.1.3*"
   ```

   `Microsoft.WindowsAppRuntime.1.3.x64` 가 보이면 OK.

> 이 런타임은 최종 사용자 PC 에도 필요하지만, 배포용 `.msixbundle` 은 자동으로 프레임워크 설치를 트리거하므로 일반 사용자는 따로 깔 필요가 없다. 개발자만 수동 설치한다.

## 4. 솔루션 열기

1. `scratch-link.sln` 더블클릭으로 VS 2026 에서 열기
2. "Migration Report" 가 뜨면 **OK** 로 닫는다. `scratch-link-mac` 이 Unsupported 로 나오는 것은 정상.
3. 솔루션 탐색기에서 `scratch-link-mac` 은 회색으로 표시된다 — 그대로 둔다.

## 5. 빌드/실행 설정

VS 상단 툴바에서:

| 항목 | 값 |
|---|---|
| Solution Configurations | **`Debug_Win`** |
| Solution Platforms | **`x64`** (또는 본인 PC 에 맞는 플랫폼) |
| Startup Project | **`scratch-link-win`** ← 중요 |

**시작 프로젝트는 `scratch-link-win` 이어야 한다.** `scratch-link-win-msix` 를 시작 프로젝트로 잡으면 F5 시 다음 에러가 난다:

```
MddBootstrapInitialize called in a process with package identity
0x80070032 지원되지 않는 요청입니다
```

이유: csproj 가 `<WindowsPackageType>None</WindowsPackageType>` (언패키지 모드) 로 빌드되는데, wapproj 가 그 EXE 를 MSIX 로 배포하면 패키지 ID 를 갖게 되어 `MddBootstrap.Initialize()` 호출이 충돌한다. `README.md` 의 "Windows platforms and installer size" 섹션 참고.

### 시작 프로젝트 설정 방법

솔루션 탐색기에서 **`scratch-link-win` 우클릭 → Set as Startup Project**. 프로젝트 이름이 굵게(bold) 변하면 적용된 것.

## 6. 워크플로우

| 목적 | Startup Project | Configuration | 결과물 |
|---|---|---|---|
| **일상 개발/디버깅 (F5)** | `scratch-link-win` | `Debug_Win` / `x64` | 언패키지 EXE 직접 실행. 평소 작업은 이걸로. |
| **MSIX 패키지 동작 확인** | `scratch-link-win-msix` | `Release_Win` / `x64` | publish profile (`Properties/PublishProfiles/win10-x64.pubxml`) 이 `WindowsPackageType=Desktop` 으로 오버라이드하여 진짜 패키지 빌드. |
| **배포용 msixbundle 생성** | `scratch-link-win-msix` | `Release_Win`, 모든 플랫폼 | x86/x64/ARM64 번들 `.msixbundle` 생성. |

## 7. MSIX 사이드로드 준비 (선택)

MSIX Debug 빌드는 임시 자체 서명 인증서로 서명된다 (`scratch-link-win-msix.wapproj` 의 `GenerateTemporaryStoreCertificate=True`). 자체 서명 MSIX 를 신뢰하려면:

- `설정 → 개인 정보 및 보안 → 개발자용` → **개발자 모드 켜기** (또는 최소한 "사이드로드 앱" 허용)

## 8. 자주 막히는 곳

| 증상 | 원인 / 해결 |
|---|---|
| `NETSDK1045: The current .NET SDK does not support targeting .NET 6.0` | .NET 6 SDK 미설치. §2 참고. |
| `Microsoft.DesktopBridge.props was not found` | MSIX Packaging Tools 누락. §1 의 ".NET 데스크톱 개발" 선택 사항 확인. |
| `Windows 10 SDK version 10.0.22621.0 was not found` | Windows 11 SDK 22621 미설치. §1 의 "WinUI 애플리케이션 개발" 선택 사항 확인. |
| `scratch-link-win-msix` 가 보이지 않거나 회색 | 솔루션 Configuration 이 `*_Win` 이 아닌 `*_Mac` 으로 되어 있을 때 흔하다. |
| F5 시 `MddBootstrapInitialize ... 0x80070032` | 시작 프로젝트가 wapproj 로 설정됨. §5 참고. |
| F5 시 "This application requires the Windows App Runtime 1.3" | 런타임 미설치. §3 참고. |
| StyleCop 경고가 에러로 처리됨 | 원본 동작. `SharedProps/StyleCop.props` 참고. 거슬리면 임시로 `TreatWarningsAsErrors` 만 끄기. |

## 9. 참고 문서

- [`README.md`](../README.md) 의 "Windows platforms and installer size" 섹션 — 패키지 형태와 배포 크기 트레이드오프 배경 설명
- [`scratch-link-win/scratch-link-win.csproj`](../scratch-link-win/scratch-link-win.csproj) — 본체 프로젝트 설정
- [`scratch-link-win-msix/scratch-link-win-msix.wapproj`](../scratch-link-win-msix/scratch-link-win-msix.wapproj) — MSIX 패키징 프로젝트 설정
- [`SharedProps/WindowsSDK.props`](../SharedProps/WindowsSDK.props) — Windows App SDK 버전 핀
