# AluxLabs Link

Scratch 3.0과 PC에 연결된 하드웨어 주변기기를 중계하는 도우미 앱.

> 본 제품은 Scratch Foundation 의 [scratch-link](https://github.com/scratchfoundation/scratch-link) (AGPL-3.0-only) 에서 파생된 Windows 전용 포크이며, 동일한 **AGPL-3.0-only** 라이선스로 배포됩니다. 변경 내역·소스 위치·상표 고지는 [NOTICE](NOTICE) ([한국어](NOTICE.ko)), 전체 라이선스 텍스트는 [LICENSE](LICENSE) 를 참조하십시오.
>
> "Scratch" 는 Scratch Foundation 의 상표입니다. 본 제품은 Scratch Foundation 과 제휴·후원·인증 관계가 없으며, 호환성 확보를 위해 원본 프로토콜을 구현한 독립적인 파생 저작물입니다.

## 원본과의 차이

- **Windows 전용** — macOS 빌드와 Safari 확장은 제외.
- **Serial 전송 추가** — BLE / Bluetooth Classic에 더해 USB 시리얼(CDC/CH340 등) 장치를 `/scratch/serial` JSON-RPC 엔드포인트로 지원. 구현은 `aluxlabs-link-common/Serial/`과 `aluxlabs-link-win/Serial/` 참고.
- **포트 20211 사용** — 원본 Scratch Link(20110/20111)와 한 PC에서 공존 가능.
- **.NET 8 / WindowsAppSDK 1.8** — 원본의 .NET 6 / WindowsAppSDK 1.3에서 업그레이드.

## 시스템 요구사항

| | 최소 사양 |
|---|---|
| Windows | Windows 10 build 17763 (1809) 이상 |
| Windows App Runtime | 1.8 (Windows 11 최신 업데이트 시 자동 설치됨) |

Windows App Runtime 1.8이 없는 경우 앱 실행 시 설치 안내가 표시됩니다. 수동 설치:

- [Windows App SDK 다운로드 페이지](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)에서 1.8 런타임 설치 파일 다운로드

## AluxLabs와 함께 쓰기

AluxLabs는 Alux 전용 Scratch 3.0입니다.

1. AluxLabs Link 실행
2. AluxLabs 열기
3. 블록 카테고리 아래 "확장 기능 추가" 선택
4. CodeTinker, Connect, CodingDrone 등 지원 확장 선택
5. 안내에 따라 주변기기 연결

## 저장소 구조

```
scratch-link/
├── aluxlabs-link-win/          # WinUI 3 앱 본체 (EXE)
│   ├── BLE/                   # Bluetooth Low Energy (Windows)
│   ├── BT/                    # Bluetooth Classic (Windows)
│   ├── Serial/                # USB 시리얼 (Windows)
│   └── Properties/PublishProfiles/  # win-x64/x86/arm64 publish 프로필
├── aluxlabs-link-win-msix/     # MSIX 패키징 프로젝트 (.wapproj)
├── aluxlabs-link-common/       # 플랫폼 공유 C# 코드 (.shproj)
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
├── Documentation/             # upstream 원본 프로토콜 문서
│   └── Alux/                  # 이 포크 전용 문서 (upstream 동기화 시 제외)
└── brand/                     # 아이콘 소스 SVG 및 빌드 스크립트
```

## 개발 환경 구성

[Documentation/Alux/WindowsDevSetup-VS2026.md](Documentation/Alux/WindowsDevSetup-VS2026.md) 참고.

## 빌드 구성

| Configuration | 용도 |
|---|---|
| `Debug_Win` | 일상 개발/디버깅 (F5) |
| `Release_Win` | 배포용 빌드 및 MSIX 패키징 |

Startup Project를 `aluxlabs-link-win-msix`로 설정하면 MSIX 패키지 빌드가 실행됩니다. 일반 디버깅은 반드시 `aluxlabs-link-win`으로 설정할 것.

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

## 다운로드

| 채널 | URL |
|---|---|
| Stable | https://scratch-link.aluxcoding.com/latest.msixbundle |
| Prerelease (개발판) | https://dev-scratch-link.aluxcoding.com/latest.msixbundle |

최신 버전 메타: `latest.json` (같은 디렉토리). 특정 버전: `archive/v<version>/...`.

> 코드사이닝 적용 상태와 임시 자체서명 안내는 아래 **코드사이닝** 섹션 참조.

## 패키징 및 배포

현재 배포 방식은 **framework-dependent**입니다. Windows App Runtime 1.8이 없는 PC에서는 설치 안내가 표시됩니다.

- **MSIX 파일(`*.msix`)**: 단일 플랫폼(x86/x64/ARM64)
- **MSIX 번들(`*.msixbundle`)**: 여러 플랫폼을 하나로 묶어 배포

self-contained 배포(런타임 번들링)로 전환하면 설치 안내가 완전히 사라지지만, 바이너리 크기가 증가합니다. AnyCPU 빌드에서는 self-contained를 지원하지 않으므로 플랫폼별 빌드(x64/x86/ARM64)가 필요합니다.

## 코드사이닝

Windows 빌드(`*.msixbundle`)는 [SignPath Foundation](https://signpath.org)의 무료 OSS 코드사이닝 서비스로 서명됩니다. SignPath Foundation은 오픈소스 프로젝트에 EV 코드사이닝 인증서를 제공하는 비영리 단체이며, 본 프로젝트는 AGPL-3.0-only 라이선스로 공개돼 있어 그 자격을 충족합니다.

*Code signing for Windows builds is provided by [SignPath Foundation](https://signpath.org), a non-profit foundation that provides free code signing certificates for open source projects.*

> 승인 절차 완료 전까지는 임시 자체서명 인증서가 사용되며, 이 기간 동안 사용자는 인증서를 Trusted Root에 수동 설치해야 설치할 수 있습니다.
