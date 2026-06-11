# 코드 서명 및 MSIX 배포 / 자동 업데이트

AluxLabs Link(`aluxlabs-link-win-msix`)를 코드 서명하고, sideload MSIX로 배포·자동 업데이트하는 절차.

> ⚠️ 토큰 비밀번호·어드민 비밀번호 등 **비밀 값은 이 문서에 적지 않는다.** 별도의 비밀 보관처(비밀번호 관리자)에 둔다.

## 0. 인증서 개요

- 종류: **Sectigo OV Code Signing** (Organization Validation)
- 형태: **하드웨어 토큰(eToken)** — 물리 USB 토큰 + `SafeNet Authentication Client` 필요
- 인증서 Subject (= 매니페스트 Publisher와 일치시킬 값): **`CN="ALUX Co.,Ltd", O="ALUX Co.,Ltd", S=Seoul, C=KR`**
- 발급자: `Sectigo Public Code Signing CA R36` / 만료: **2027-07-01** (갱신 필요 시점)
- 지문(SHA1): `EB74741683C9CDCE4457571A7EDD075A835B02C6` — `signtool /sha1`에 사용
- 동작 제약:
  - 서명하려면 **토큰을 PC에 꽂고 SafeNet 클라이언트 + 토큰 비밀번호**가 있어야 한다.
  - **토큰 비밀번호 3회 실패 시 잠김** → Sectigo 지원팀으로만 해제. 비밀번호 창은 복사·붙여넣기 가능(오타 방지 권장).
  - SafeNet Authentication Client 다운로드: <https://www.sectigo.com/knowledge-base/detail/SafeNet-Authentication-Client-Download-for-Sectigo-Certificates-on-eToken/kA03l000000o6kL>
  - SafeNet 설치는 **Typical**(Microsoft Crypto Providers 포함) 선택 → `signtool`이 토큰 키에 접근 가능해진다.

### OV의 한계 (알고 시작할 것)

- **SmartScreen 평판은 즉시 생기지 않는다.** EV와 달리 다운로드·설치가 누적되어야 "알 수 없는 게시자" 경고가 사라진다. 초기 사용자는 경고를 볼 수 있다.
- **하드웨어 토큰이라 CI 자동 서명이 어렵다.** 토큰을 꽂은 머신에서만 서명되므로 **로컬 수동 서명**으로 운영한다.

### 배포 트랙 (한 버킷, 파일명으로 분리)

서명 주체가 둘이지만 **버킷은 `scratch-link`(prod)/`dev-scratch-link`(dev)로 공통**이다. 파일명이 달라 충돌하지 않는다.

| 트랙 | 서명 | 파일 | 주체 |
|---|---|---|---|
| **실제 배포** | **USB 토큰(Sectigo)** | `AluxLabsLink.appinstaller` + `AluxLabs-Link-<버전>.msixbundle` | **수동** (이 문서) |
| CI/CD 테스트 | SignPath(신청 중) / 임시 인증서 | `latest.msixbundle` + `latest.json` + `archive/` | GitHub Actions (`release.yml`) |

- 두 트랙은 **인증서 Subject가 달라 Publisher도 다르다** → 서로 자동 업데이트 안 됨(의도된 분리).
- CI 버킷은 `scripts/aws/setup-cdn.sh`가 만든 것. **CI 파일(`latest.*`, `archive/`)은 건드리지 않는다.**
- (`scratch-assets` 버킷도 검토했으나 공개 DNS가 없어 제외. `scratch-link`만 CloudFront로 공개 서빙됨.)

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

### `.appinstaller` 파일 (템플릿에서 자동 생성)

`make`가 staging 된 번들 파일명에서 버전을 읽어 prod/dev 두 파일을 생성한다(수동 편집 없음). 정본은 템플릿이다:

- 템플릿: [`AluxLabsLink.appinstaller.template`](../../aluxlabs-link-win-msix/AluxLabsLink.appinstaller.template) — 호스트/버전/번들 placeholder
- 생성물(gitignore): `dist/upload/AluxLabsLink.appinstaller` (prod) / `dist/upload/AluxLabsLink.dev.appinstaller` (dev)
- 생성: `make appinstaller` (또는 `make sync-s3`/`sync-s3-dev` 가 업로드 직전 자동 생성)

스키마는 `2017/2` (Windows 1809+ 호환 — 매니페스트 MinVersion과 동일). `Publisher`는 인증서 Subject와 일치.

### 버전 — 한 곳만 (Version.props)

`.appinstaller`의 버전·번들 파일명은 **빌드된 번들과 자동으로 일치**한다 — `make`가 staging 된 번들명(`AluxLabs-Link-<버전>.msixbundle`)에서 버전을 그대로 주입하기 때문. 손으로 맞출 곳은 없다.

릴리스 버전(Major.Minor.Patch)은 **`SharedProps/Version.props` 한 곳**이 단일 소스다(`ScratchVersion.targets`가 읽어 assembly·매니페스트·번들명에 전파). 4번째 Build 자리는 커밋 수로 자동.

```powershell
make set-version VERSION=1.2.0   # 또는 make release-patch / release-minor
make show-version                # 산출될 quad 확인
```

> **`<AppInstaller>`의 `Uri`(고정 URL)는 불변** — 설치된 클라이언트가 기억하는 진입점이다. 템플릿에 고정돼 있고, 버전·번들명만 주입된다.

빌드된 번들의 실제 버전·Publisher 확인:

```powershell
Get-AppLockerFileInformation -Path .\AluxLabs-Link-x.y.z.msixbundle | Select-Object -ExpandProperty Publisher
```

### 호스팅 — `scratch-link.aluxcoding.com` 루트 (확정)

토큰 배포는 **`scratch-link.aluxcoding.com`(prod) / `dev-scratch-link.aluxcoding.com`(dev) 루트**에 `aws s3 cp`로 수동 업로드한다. (이 도메인만 CloudFront로 공개 서빙됨 — `scratch-assets`는 공개 DNS가 없어 제외.)

```
scratch-link.aluxcoding.com/
  AluxLabsLink.appinstaller          # 고정 URL (진입점, 불변)
  AluxLabs-Link-x.y.z.msixbundle     # 토큰 서명된 번들 (버전별)
```

진입점 URL: `https://scratch-link.aluxcoding.com/AluxLabsLink.appinstaller`

- **`.appinstaller` URL은 영원히 고정**. `.msixbundle` URL은 버전마다 바뀌어도 됨.
- **MIME 타입 필수** — 콘솔 업로드는 `binary/octet-stream`이 붙어 깨진다. 반드시 `--content-type` 지정:
  - `.appinstaller` → `application/appinstaller`
  - `.msixbundle` → `application/vnd.ms-appx`
- 업로드 후 **CloudFront 무효화** 필수 (안 하면 옛 캐시가 잘못된 MIME로 남음).
  - 배포 ID: prod `E3HEXR4KAZLITV`, dev `E1WMSQXPP9L5YF`

업로드 + 무효화 명령 (CLI 자격증명 필요 — IAM 정책 `scripts/aws/policies/iam-policy.json.tpl`):

```powershell
$v = "1.0.0.1028"   # 실제 빌드 버전
$b = "scratch-link.aluxcoding.com"   # dev면 dev-scratch-link.aluxcoding.com
aws s3 cp "dist\upload\AluxLabs-Link-$v.msixbundle" "s3://$b/AluxLabs-Link-$v.msixbundle" --content-type application/vnd.ms-appx --cache-control "public, max-age=31536000, immutable"
aws s3 cp "dist\upload\AluxLabsLink.appinstaller"   "s3://$b/AluxLabsLink.appinstaller"   --content-type application/appinstaller --cache-control "public, max-age=300"
aws cloudfront create-invalidation --distribution-id E3HEXR4KAZLITV --paths "/AluxLabsLink.appinstaller" "/AluxLabs-Link-$v.msixbundle"
```

검증 (HTTP HEAD로 Content-Type 확인):
```powershell
Invoke-WebRequest -Method Head "https://scratch-link.aluxcoding.com/AluxLabsLink.appinstaller" -UseBasicParsing | % { $_.Headers['Content-Type'] }
```

## 5. 신규 버전 릴리스 체크리스트 (토큰 수동)

1. [ ] `make set-version VERSION=x.y.z` (또는 release-patch/minor) — 버전 단일 소스 갱신
2. [ ] 토큰 연결 + SafeNet 클라이언트 실행 확인
3. [ ] Release 구성으로 `.msixbundle` 빌드 (self-contained x86|x64) → `AluxLabs-Link-x.y.z.<커밋수>.msixbundle`
4. [ ] `signtool`로 서명 — **`/sha1 <썸프린트>` 권장** (`/n` 이름 매칭은 간헐 실패)
5. [ ] `signtool verify /pa`로 서명 검증
6. [ ] 서명된 번들을 `dist/upload/`로 복사
7. [ ] `make sync-s3` (dev면 `sync-s3-dev`) — appinstaller 자동 생성 + 업로드 + CloudFront 무효화
8. [ ] HTTP HEAD로 Content-Type 검증 + 기존 설치본 자동 업데이트 확인
