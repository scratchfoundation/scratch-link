# 코드 서명 및 MSIX 배포 / 자동 업데이트

AluxLabs Link(`aluxlabs-link-win-msix`)를 코드 서명하고, sideload MSIX로 배포·자동 업데이트하는 절차.

> ⚠️ 토큰 비밀번호·어드민 비밀번호 등 **비밀 값은 이 문서에 적지 않는다.** 별도의 비밀 보관처(비밀번호 관리자)에 둔다.

## 0. 인증서 개요

- 종류: **Sectigo OV Code Signing** (Organization Validation)
- 형태: **하드웨어 토큰(eToken)** — 물리 USB 토큰 + `SafeNet Authentication Client` 필요
- 동작 제약:
  - 서명하려면 **토큰을 PC에 꽂고 SafeNet 클라이언트 + 토큰 비밀번호**가 있어야 한다.
  - **토큰 비밀번호 3회 실패 시 잠김** → Sectigo 지원팀으로만 해제. 자동화 실패 루프 주의.
  - SafeNet Authentication Client 다운로드: <https://www.sectigo.com/knowledge-base/detail/SafeNet-Authentication-Client-Download-for-Sectigo-Certificates-on-eToken/kA03l000000o6kL>

### OV의 한계 (알고 시작할 것)

- **SmartScreen 평판은 즉시 생기지 않는다.** EV와 달리 다운로드·설치가 누적되어야 "알 수 없는 게시자" 경고가 사라진다. 초기 사용자는 경고를 볼 수 있다.
- **하드웨어 토큰이라 CI 자동 서명이 어렵다.** 현재는 **로컬 수동 서명**이 현실적. (자동화하려면 셀프호스트 러너에 토큰 연결 또는 클라우드 HSM 서명 서비스가 필요.)

## 1. 서명 도구 — SignTool

표준 도구는 **`signtool.exe`** (Windows SDK 포함).

- 위치: `C:\Program Files (x86)\Windows Kits\10\bin\<버전>\x64\signtool.exe`
- PATH에 없으므로 **Visual Studio의 "Developer PowerShell" / "Developer Command Prompt"** 에서 실행하면 자동으로 잡힌다.

서명:

```powershell
signtool sign /fd SHA256 /a /n "ALUX Inc." `
  /tr http://timestamp.sectigo.com /td SHA256 `
  AluxLabsLink.msixbundle
```

- `/a` — 적합한 인증서 자동 선택 (토큰 인증서)
- `/n "ALUX Inc."` — Subject 이름으로 인증서 지정
- `/tr` + `/td SHA256` — 타임스탬프. **필수** (인증서 만료 후에도 서명 유효 유지)
- `/fd SHA256` — 파일 다이제스트 알고리즘

확인:

```powershell
signtool verify /pa /v AluxLabsLink.msixbundle
```

## 2. ⚠️ Publisher 일치 규칙 (MSIX 최대 함정)

MSIX는 **서명 인증서의 Subject DN과 매니페스트 `Publisher`가 문자 그대로 정확히 일치**해야 한다. 공백·쉼표·순서까지 동일해야 하며, 안 맞으면 서명 실패 또는 설치 거부.

현재 [`Package.appxmanifest`](../../aluxlabs-link-win-msix/Package.appxmanifest):

```xml
<Identity Name="ALUXInc.AluxLabsLink" Publisher="CN=ALUX Inc." Version="1.0.0.0" />
```

OV 인증서의 실제 Subject는 보통 더 길다 (예: `CN=ALUX Inc., O=ALUX Inc., L=..., S=..., C=KR`).

**인증서 도착 후 절차:**

1. 실제 Subject 전체 문자열 확인:
   ```powershell
   certutil -user -store My
   # 또는 SafeNet Authentication Client에서 인증서 보기
   ```
2. 매니페스트의 `Publisher`를 그 값과 **완전히 동일하게** 수정.
3. (자동 업데이트 사용 시) `.appinstaller`의 `Publisher`도 동일하게 맞춘다 (4절).

## 3. MSIX 서명 켜기 (wapproj 설정)

현재 [`aluxlabs-link-win-msix.wapproj`](../../aluxlabs-link-win-msix/aluxlabs-link-win-msix.wapproj) 는 **서명 OFF + 임시 인증서** 상태:

```xml
<AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
<GenerateTemporaryStoreCertificate>True</GenerateTemporaryStoreCertificate>
```

배포 빌드에서는 임시 인증서 대신 실제 OV 토큰으로 서명한다. 권장 방식은 **빌드는 서명 없이(`AppxPackageSigningEnabled=false`) 번들만 만들고, 빌드 후 `signtool`로 별도 서명**하는 것 (토큰 비밀번호 입력 시점을 분리할 수 있어 CI/수동 모두 유연).

VS GUI로 할 경우: wapproj 우클릭 → **게시(Publish) → 앱 패키지 만들기** 마법사에서 토큰 인증서를 선택해 서명할 수도 있다.

## 4. 자동 업데이트 — `.appinstaller` (App Installer)

별도 업데이트 서버/백엔드 로직이 **필요 없다.** Windows 내장 App Installer가 버전 비교·다운로드·적용을 모두 처리한다. 우리는 **정적 파일만 호스팅**한다.

### 동작 흐름

1. 사용자는 `.msixbundle`이 아니라 **`.appinstaller` URL로 최초 설치**한다.
2. App Installer가 해당 `.appinstaller` URI를 기억한다.
3. 앱 실행 시(또는 백그라운드 작업) Windows가 `.appinstaller`를 다시 받아 **`Version`과 설치된 버전을 스스로 비교**한다.
4. 더 높으면 새 번들을 받아 적용한다.

### `.appinstaller` 예시

```xml
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
  xmlns="http://schemas.microsoft.com/appx/appinstaller/2018"
  Uri="https://download.alux.../AluxLabsLink.appinstaller"
  Version="1.2.0.0">
  <MainBundle
    Name="ALUXInc.AluxLabsLink"
    Publisher="CN=ALUX Inc., O=..., C=KR"
    Version="1.2.0.0"
    Uri="https://download.alux.../AluxLabsLink_1.2.0.0.msixbundle" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="0" />
    <AutomaticBackgroundTask />
  </UpdateSettings>
</AppInstaller>
```

### 켜는 법

[`aluxlabs-link-win-msix.wapproj`](../../aluxlabs-link-win-msix/aluxlabs-link-win-msix.wapproj) 에서:

```xml
<GenerateAppInstallerFile>True</GenerateAppInstallerFile>   <!-- 현재 False -->
<AppInstallerUri>https://download.alux.../</AppInstallerUri>
<HoursBetweenUpdateChecks>0</HoursBetweenUpdateChecks>      <!-- 0 = 실행 시마다 확인. 이미 설정됨 -->
```

`GenerateAppInstallerFile=True`로 켜면 MSBuild가 패키지 버전과 `.appinstaller`의 `Version`을 자동 동기화한다. 패키지 버전은 커밋 수로 자동 증가하도록 되어 있으므로(MSIX 패키징 설정 참조), 버전 숫자를 손으로 고칠 일이 없다.

### 호스팅 선택지 (미정)

업로드 위치는 아직 미정. **`.appinstaller`의 URL은 영원히 고정**이어야 한다(설치된 클라이언트가 그 URL을 기억해 업데이트를 확인). 반면 `.msixbundle` URL은 버전마다 바뀌어도 된다.

| 방식 | 비용 | 비고 |
|---|---|---|
| **GitHub Pages + Releases** | 무료 | 이미 GitHub 사용. `.appinstaller`는 Pages(고정 URL), 번들은 Releases 에셋(버전 태그별) |
| **AWS S3 + CloudFront** | 저렴 | URL 고정·MIME·HTTPS 설정 가능, 커스텀 도메인 가능 |

### 호스팅 요구사항 (공통)

- 일반 웹서버면 **MIME 타입 등록 필수**:
  - `.appinstaller` → `application/appinstaller`
  - `.msixbundle` → `application/msix`
- **HTTPS 강력 권장.**

## 5. 신규 버전 릴리스 체크리스트

1. [ ] 토큰 연결 + SafeNet 클라이언트 실행 확인
2. [ ] Release 구성으로 `.msixbundle` 빌드
3. [ ] `signtool`로 서명 (1절) — Publisher 일치 확인(2절)
4. [ ] `signtool verify /pa`로 서명 검증
5. [ ] `.appinstaller` + `.msixbundle`을 호스팅 위치에 업로드 (버전 동기화 확인)
6. [ ] 기존 설치본에서 자동 업데이트 동작 확인
