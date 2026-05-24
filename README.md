# Alux Scratch Link

Scratch 3.0과 PC에 연결된 하드웨어 주변기기를 중계하는 도우미 앱.
[scratchfoundation/scratch-link](https://github.com/scratchfoundation/scratch-link)의 **Windows 전용 포크**이며, 원본의 AGPL-3.0-only 라이선스를 그대로 따릅니다.

## 원본과의 차이

- **Windows 전용** — macOS 빌드와 Safari 확장은 제외.
- **Serial 전송 추가** — BLE / Bluetooth Classic에 더해 USB 시리얼(CDC/CH340 등) 장치를 `/scratch/serial` JSON-RPC 엔드포인트로 지원. 구현은 `scratch-link-common/Serial/`과 `scratch-link-win/Serial/` 참고.
- **포트 20211 사용** — 원본 Scratch Link(20110/20111)와 한 PC에서 공존 가능.
- **.NET 8 / WindowsAppSDK 1.8** — 원본의 .NET 6 / WindowsAppSDK 1.3에서 업그레이드.

## 시스템 요구사항

| | 최소 사양 |
|---|---|
| Windows | Windows 10 build 17763 (1809) 이상 |
| Windows App Runtime | 1.8 (Windows 11 최신 업데이트 시 자동 설치됨) |

Windows App Runtime 1.8이 없는 경우 앱 실행 시 설치 안내가 표시됩니다. 수동 설치:

- [Windows App SDK 다운로드 페이지](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)에서 1.8 런타임 설치 파일 다운로드

## Scratch 3.0과 함께 쓰기

1. Alux Scratch Link 실행
2. [Scratch 3.0](https://scratch.mit.edu) 열기
3. 블록 카테고리 아래 "확장 기능 추가" 선택
4. micro:bit, LEGO EV3 등 지원 확장 선택
5. 안내에 따라 주변기기 연결

## 저장소 구조

```
scratch-link/
├── scratch-link-win/          # WinUI 3 앱 본체 (EXE)
│   ├── BLE/                   # Bluetooth Low Energy (Windows)
│   ├── BT/                    # Bluetooth Classic (Windows)
│   ├── Serial/                # USB 시리얼 (Windows)
│   └── Properties/PublishProfiles/  # win-x64/x86/arm64 publish 프로필
├── scratch-link-win-msix/     # MSIX 패키징 프로젝트 (.wapproj)
├── scratch-link-common/       # 플랫폼 공유 C# 코드 (.shproj)
│   ├── BLE/                   # BLE 세션 공통 로직
│   ├── BT/                    # BT 세션 공통 로직
│   ├── Serial/                # 시리얼 세션 공통 로직
│   ├── JsonRpc/               # JSON-RPC 2.0 구현
│   └── Extensions/            # 유틸리티 확장 메서드
├── SharedProps/               # 공유 MSBuild 프로퍼티
│   ├── WindowsSDK.props       # WindowsAppSDK 버전 핀
│   ├── ScratchVersion.targets # Git 기반 버전 자동 생성
│   ├── CommonPackageRefs.props # 공유 NuGet 패키지
│   └── StyleCop.props         # 코드 스타일 분석
├── Documentation/             # 프로토콜 문서 (Architecture, Bluetooth, Serial 등)
└── brand/                     # 아이콘 소스 SVG 및 빌드 스크립트
```

## 개발 환경 구성

[../documents/WindowsDevSetup-VS2026.md](../documents/WindowsDevSetup-VS2026.md) 참고.

요약:

1. Visual Studio 2026 워크로드: `.NET 데스크톱 개발`, `WinUI 애플리케이션 개발`, `C++ 데스크톱 개발`
2. Solution Configuration: `Debug_Win`, Platform: `x64`
3. Startup Project: `scratch-link-win`
4. F5로 실행 → 트레이 아이콘 확인

## 빌드 구성

| Configuration | 용도 |
|---|---|
| `Debug_Win` | 일상 개발/디버깅 (F5) |
| `Release_Win` | 배포용 빌드 및 MSIX 패키징 |

Startup Project를 `scratch-link-win-msix`로 설정하면 MSIX 패키지 빌드가 실행됩니다. 일반 디버깅은 반드시 `scratch-link-win`으로 설정할 것.

## 버전 번호

`SharedProps/ScratchVersion.targets`에서 git 메타데이터를 기반으로 자동 생성됩니다.

- git semver 태그가 없으면 `1.0.0.<커밋수>` 형태
- 정식 릴리즈 시 `git tag v1.1.0`처럼 태그를 찍으면 해당 버전을 따라감

상세 버전 문자열은 트레이 메뉴의 버전 항목을 클릭해 클립보드로 복사할 수 있습니다.

## 브랜드 자산

모든 아이콘은 [brand/labs-l.svg](brand/labs-l.svg)에서 파생됩니다. SVG 변경 시:

```
pip install Pillow   # 최초 1회
python brand/build_icons.py
```

생성물(ICO/PNG)은 커밋되어 있으므로 일반 빌드 시에는 실행 불필요.

## 패키징 및 배포

현재 배포 방식은 **framework-dependent**입니다. Windows App Runtime 1.8이 없는 PC에서는 설치 안내가 표시됩니다.

- **MSIX 파일(`*.msix`)**: 단일 플랫폼(x86/x64/ARM64)
- **MSIX 번들(`*.msixbundle`)**: 여러 플랫폼을 하나로 묶어 배포

self-contained 배포(런타임 번들링)로 전환하면 설치 안내가 완전히 사라지지만, 바이너리 크기가 증가합니다. AnyCPU 빌드에서는 self-contained를 지원하지 않으므로 플랫폼별 빌드(x64/x86/ARM64)가 필요합니다.
