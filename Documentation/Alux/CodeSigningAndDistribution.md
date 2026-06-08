# 코드 서명 및 MSIX 배포 / 자동 업데이트

AluxLabs Link(`aluxlabs-link-win-msix`)를 코드 서명하고, sideload MSIX로 배포·자동 업데이트하는 절차.

> ⚠️ 토큰 비밀번호·어드민 비밀번호 등 **비밀 값은 이 문서에 적지 않는다.** 별도의 비밀 보관처(비밀번호 관리자)에 둔다.

## 0. 인증서 개요

- 종류: **Sectigo OV Code Signing** (Organization Validation)
- 형태: **하드웨어 토큰(eToken)** — 물리 USB 토큰 + `SafeNet Authentication Client` 필요
- 인증서 Subject (= 매니페스트 Publisher와 일치시킬 값): **`CN="ALUX Co.,Ltd", O="ALUX Co.,Ltd", S=Seoul, C=KR`**
- 발급자: `Sectigo Public Code Signing CA R36` / 만료: **2027-07-01** (갱신 필요 시점)
- 동작 제약:
  - 서명하려면 **토큰을 PC에 꽂고 SafeNet 클라이언트 + 토큰 비밀번호**가 있어야 한다.
  - **토큰 비밀번호 3회 실패 시 잠김** → Sectigo 지원팀으로만 해제. 비밀번호 창은 복사·붙여넣기 가능(오타 방지 권장).
  - SafeNet Authentication Client 다운로드: <https://www.sectigo.com/knowledge-base/detail/SafeNet-Authentication-Client-Download-for-Sectigo-Certificates-on-eToken/kA03l000000o6kL>
  - SafeNet 설치는 **Typical**(Microsoft Crypto Providers 포함) 선택 → `signtool`이 토큰 키에 접근 가능해진다.

### OV의 한계 (알고 시작할 것)

- **SmartScreen 평판은 즉시 생기지 않는다.** EV와 달리 다운로드·설치가 누적되어야 "알 수 없는 게시자" 경고가 사라진다. 초기 사용자는 경고를 볼 수 있다.
- **하드웨어 토큰이라 CI 자동 서명이 어렵다.** 토큰을 꽂은 머신에서만 서명되므로 **로컬 수동 서명**으로 운영한다.

### 배포 트랙 분리 (중요)

서명 주체가 둘이라 **버킷·신원을 분리**한다. 섞이면 패키지 신원이 충돌해 자동 업데이트가 깨진다.

| 트랙 | 서명 | 버킷 | 주체 |
|---|---|---|---|
| **실제 배포** | **USB 토큰(Sectigo)** | `scratch-assets.aluxcoding.com/link/` (+ dev: `dev-scratch-assets.../link/`) | **수동** (이 문서) |
| CI/CD 테스트 | SignPath(신청 중) / 임시 인증서 | `scratch-link.aluxcoding.com` / `dev-scratch-link...` | GitHub Actions (`release.yml`) |

- 두 트랙은 **인증서 Subject가 달라 Publisher도 다르다** → 서로 자동 업데이트 안 됨(의도된 분리).
- CI 버킷(`scratch-link`)은 `scripts/aws/setup-cdn.sh`가 관리. **토큰 배포는 그쪽을 건드리지 않는다.**

## 1. 서명 도구 — SignTool

표준 도구는 **`signtool.exe`** (Windows SDK 포함).

- 위치: `C:\Program Files (x86)\Windows Kits\10\bin\<버전>\x64\signtool.exe`
- PATH에 없으므로 **Visual Studio의 "Developer PowerShell" / "Developer Command Prompt"** 에서 실행하면 자동으로 잡힌다.

서명:

```powershell
signtool sign /fd SHA256 /a /n "ALUX Co.,Ltd" `
  /tr http://timestamp.sectigo.com /td SHA256 `
  AluxLabsLink.msixbundle
```

- `/a` — 적합한 인증서 자동 선택 (토큰 인증서)
- `/n "ALUX Co.,Ltd"` — Subject 이름으로 인증서 지정
- `/tr` + `/td SHA256` — 타임스탬프. **필수** (인증서 만료 후에도 서명 유효 유지)
- `/fd SHA256` — 파일 다이제스트 알고리즘
- 실행 시 **SafeNet 비밀번호 창**이 팝업된다. (검증됨: 일반 PE 파일 시험 서명 성공 — 체인·타임스탬프 정상)

확인:

```powershell
signtool verify /pa /v AluxLabsLink.msixbundle
```

## 2. ⚠️ Publisher 일치 규칙 (MSIX 최대 함정)

MSIX는 **서명 인증서의 Subject DN과 매니페스트 `Publisher`가 문자 그대로 정확히 일치**해야 한다. 공백·쉼표·순서까지 동일해야 하며, 안 맞으면 서명 실패 또는 설치 거부.

**확정값** — [`Package.appxmanifest`](../../aluxlabs-link-win-msix/Package.appxmanifest)의 `Publisher`는 인증서 Subject에 맞춰져 있다. 콤마가 값 안에 있어 XML에서는 따옴표를 `&quot;`로 이스케이프한다:

```xml
<Identity
    Name="ALUXInc.AluxLabsLink"
    Publisher="CN=&quot;ALUX Co.,Ltd&quot;, O=&quot;ALUX Co.,Ltd&quot;, S=Seoul, C=KR"
    Version="1.0.0.0" />
```

- `Publisher`(서명 신원)는 인증서와 일치 — **변경 금지**(바꾸면 패키지 신원이 바뀌어 자동 업데이트 끊김).
- 사용자에게 보이는 발행자 이름은 별개인 `<PublisherDisplayName>ALUX Inc.</PublisherDisplayName>` (브랜드명, sideload에서는 인증서와 무관).
- `.appinstaller`의 `Publisher`도 같은 값으로 맞춰져 있다 (4절).

> 인증서 Subject 재확인이 필요하면: `certutil -user -store My` 또는 SafeNet Authentication Client에서 인증서 보기.

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

### `.appinstaller` 파일 (확정·커밋됨)

수동 운영이므로 MSBuild 자동 생성 대신 파일을 직접 관리한다. 저장소에 둔 정본:

- prod: [`dist/AluxLabsLink.appinstaller`](../../aluxlabs-link-win-msix/dist/AluxLabsLink.appinstaller) → `scratch-assets.aluxcoding.com/link/`
- dev: [`dist/AluxLabsLink.dev.appinstaller`](../../aluxlabs-link-win-msix/dist/AluxLabsLink.dev.appinstaller) → `dev-scratch-assets.aluxcoding.com/link/`

스키마는 `2017/2` (Windows 1809+ 호환 — 매니페스트 MinVersion과 동일). `Publisher`는 인증서 Subject와 일치.

### ⚠️ 릴리스마다 고칠 곳 — 버전 3곳

`.appinstaller`의 버전이 **실제 빌드된 번들 버전과 정확히 일치**해야 Windows가 비교한다. 새 릴리스마다 다음 3곳을 그 버전으로 바꾼다:

1. `<AppInstaller ... Version="x.y.z.b">` — 루트
2. `<MainBundle ... Version="x.y.z.b">` — 번들
3. `<MainBundle ... Uri=".../AluxLabs-Link-x.y.z.b.msixbundle">` — 파일명

> **`<AppInstaller>`의 `Uri`(고정 URL)는 절대 변경 금지** — 설치된 클라이언트가 기억하는 진입점이다. 바꾸는 건 버전과 번들 파일명뿐.

버전은 git 태그 기반으로 산출된다(`ScratchVersion.targets`): 태그 없으면 `1.0.0.<커밋수>`, 정식 릴리스는 `git tag v1.2.0` → `1.2.0.x`. 빌드된 번들의 실제 버전·Publisher 확인:

```powershell
Get-AppLockerFileInformation -Path .\AluxLabs-Link-x.y.z.msixbundle | Select-Object -ExpandProperty Publisher
```

### 호스팅 — `scratch-assets/link/` (확정)

토큰 배포는 `scratch-assets.aluxcoding.com/link/`(dev는 `dev-scratch-assets.../link/`)에 **수동 업로드**한다. 이 버킷은 이미 Scratch 자산(`driver/`, `firmware/`, `sb3/` 등)을 서빙 중이며 CI(`setup-cdn.sh`)가 건드리지 않는 곳이라 충돌이 없다.

```
scratch-assets.aluxcoding.com/
  link/
    AluxLabsLink.appinstaller          # 고정 URL (진입점, 불변)
    AluxLabs-Link-x.y.z.msixbundle     # 토큰 서명된 번들 (버전별)
```

- **`.appinstaller` URL은 영원히 고정**. `.msixbundle` URL은 버전마다 바뀌어도 됨.
- **MIME 타입 필수** (이 버킷엔 아직 MSIX MIME이 없을 수 있음 → 업로드 시 `--content-type`으로 지정):
  - `.appinstaller` → `application/appinstaller`
  - `.msixbundle` → `application/vnd.ms-appx`
- CloudFront HTTPS 서빙 전제.

## 5. 신규 버전 릴리스 체크리스트 (토큰 수동)

1. [ ] 토큰 연결 + SafeNet 클라이언트 실행 확인
2. [ ] Release 구성으로 `.msixbundle` 빌드
3. [ ] `signtool`로 서명 (1절) — Publisher 일치(2절)
4. [ ] `signtool verify /pa`로 서명 검증
5. [ ] `.appinstaller`의 **버전 3곳 + 번들 파일명** 갱신 (위 규칙)
6. [ ] `scratch-assets/link/`에 `.appinstaller` + 서명된 번들 업로드 (`--content-type` 지정)
7. [ ] 기존 설치본에서 자동 업데이트 동작 확인
