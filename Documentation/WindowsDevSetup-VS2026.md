# Windows 개발 환경 세팅 (Visual Studio 2026)

Visual Studio 2026으로 Alux Scratch Link를 빌드/디버깅하기 위한 환경 세팅 절차.

## 0. 사전 정보

- 솔루션 파일 `scratch-link.sln`은 VS 2026에서 그대로 열 수 있다. **버전 변환 프롬프트가 떠도 변환하지 말 것** (sln 포맷이 바뀌어 PR이 지저분해진다).
- 윈도우 관련 프로젝트:
  - `scratch-link-win` — WinUI 3 기반 본체 EXE (net8.0-windows)
  - `scratch-link-win-msix` — `.wapproj` (Desktop Bridge) 형식의 MSIX 패키징 프로젝트
  - `scratch-link-common` — 공유 C# 코드 (`.shproj`, 공유 아이템 프로젝트)
- `scratch-link-mac`은 솔루션을 열면 "Unsupported"로 표시되는데 **정상이다**. Windows VS에서는 어차피 빌드하지 않으므로 무시한다 (솔루션에서 제거하지 말 것).

## 1. Visual Studio Installer 워크로드

VS Installer를 열고 **수정(Modify)**으로 다음 워크로드를 체크한다.

### 워크로드 (Workloads 탭)

- ☑ **.NET 데스크톱 개발** (.NET desktop development)
- ☑ **C++를 사용한 데스크톱 개발** (Desktop development with C++)
- ☑ **WinUI 애플리케이션 개발** (WinUI application development)

### 각 워크로드의 선택 사항

**`.NET 데스크톱 개발`의 선택 사항:**

- ☑ **MSIX Packaging Tools** — `.wapproj` 빌드에 필수.

**`WinUI 애플리케이션 개발`의 선택 사항:**

- ☑ **Windows 11 SDK (10.0.22621.0)** — `scratch-link-win.csproj`의 `TargetFramework=net8.0-windows10.0.22621.0`이 요구하는 SDK.

> `.NET 8 SDK`는 VS 2026에 포함되어 있으므로 별도 설치가 필요 없다.

## 2. Windows App Runtime 확인

이 프로젝트는 `Microsoft.WindowsAppSDK 1.8`을 framework-dependent 모드로 참조한다. F5 실행 시 **Windows App Runtime 1.8이 시스템에 설치되어 있어야** 한다.

**Windows 11 최신 업데이트 적용 환경이라면 이미 설치되어 있을 가능성이 높다.**

확인 방법:

```powershell
Get-AppxPackage -Name "Microsoft.WindowsAppRuntime.1.8*"
```

`Microsoft.WindowsAppRuntime.1.8_*` 패키지가 보이면 OK.

없으면 [Windows App SDK 다운로드 페이지](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)에서 1.8 런타임 설치 파일을 받아 실행한다.

## 3. 솔루션 열기

1. `scratch-link.sln` 더블클릭으로 VS 2026에서 열기
2. "Migration Report"가 뜨면 **OK**로 닫는다. `scratch-link-mac`이 Unsupported로 나오는 것은 정상.
3. **솔루션 탐색기**에서 `scratch-link-win`을 우클릭 → **Set as Startup Project**. 프로젝트 이름이 굵게(bold) 변하면 적용된 것.

## 4. 빌드/실행 설정

VS 상단 툴바에서:

| 항목 | 값 |
|---|---|
| Solution Configurations | **`Debug_Win`** |
| Solution Platforms | **`x64`** (또는 본인 PC에 맞는 플랫폼) |
| Startup Project | **`scratch-link-win`** |

> **시작 프로젝트는 반드시 `scratch-link-win`이어야 한다.** `scratch-link-win-msix`로 F5를 누르면 패키지 ID 충돌로 `MddBootstrapInitialize 0x80070032` 에러가 난다.

이후 **F5**로 빌드 및 실행. 트레이 아이콘이 나타나면 정상 동작.

## 5. 워크플로우

| 목적 | Startup Project | Configuration | 결과물 |
|---|---|---|---|
| **일상 개발/디버깅 (F5)** | `scratch-link-win` | `Debug_Win` / `x64` | 언패키지 EXE 직접 실행 |
| **MSIX 패키지 빌드** | `scratch-link-win-msix` | `Release_Win` / `x64` | publish profile (`win-x64.pubxml`)로 MSIX 생성 |
| **배포용 msixbundle** | `scratch-link-win-msix` | `Release_Win`, 전 플랫폼 | x86/x64/ARM64 `.msixbundle` 생성 |

## 6. 알려진 이슈

| 증상 | 원인 / 해결 |
|---|---|
| `Microsoft.DesktopBridge.props was not found` | MSIX Packaging Tools 누락. §1의 ".NET 데스크톱 개발" 선택 사항 확인. |
| `Windows 10 SDK version 10.0.22621.0 was not found` | Windows 11 SDK 22621 미설치. §1의 "WinUI 애플리케이션 개발" 선택 사항 확인. |
| F5 시 `MddBootstrapInitialize ... 0x80070032` | 시작 프로젝트가 wapproj로 설정됨. §4 참고. |
| F5 시 "This application requires the Windows App Runtime 1.8" | 런타임 미설치. §2 참고. |
| `scratch-link-win-msix`가 회색으로 비활성화 | Configuration이 `*_Win`이 아닌 다른 것으로 되어 있음. `Debug_Win` 또는 `Release_Win`으로 전환. |
| CS 빌드 에러 (System.Management, Fleck 등 누락) | NuGet 캐시 불일치. CLI에서 `dotnet restore --force scratch-link-win/scratch-link-win.csproj` 실행 후 VS 재시작. |
| 빌드 시 `프로젝트에 'GitVersion' 대상이 없습니다` | VS 2026 MSBuild의 NuGetPackageRoot 경로 차이로 GitInfo.targets가 로드 안 되는 경우. `SharedProps/ScratchVersion.targets`에 fallback 타겟이 있어 정상 동작하므로 무시해도 된다. |

## 7. 참고

- [`scratch-link-win/scratch-link-win.csproj`](../scratch-link-win/scratch-link-win.csproj) — 본체 프로젝트 설정
- [`scratch-link-win-msix/scratch-link-win-msix.wapproj`](../scratch-link-win-msix/scratch-link-win-msix.wapproj) — MSIX 패키징 설정
- [`SharedProps/WindowsSDK.props`](../SharedProps/WindowsSDK.props) — Windows App SDK 버전 핀
- [`SharedProps/ScratchVersion.targets`](../SharedProps/ScratchVersion.targets) — 버전 자동 생성 로직
