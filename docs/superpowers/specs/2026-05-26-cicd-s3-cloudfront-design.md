# CI/CD → S3 → CloudFront 배포 파이프라인 설계

- 작성일: 2026-05-26
- 작성자: jysong (with Claude)
- 상태: 초안 (사용자 리뷰 대기)

## 1. 목표

git tag 푸시 한 번으로 다음이 자동 수행된다.

1. Windows에서 `.msixbundle` 빌드
2. AWS S3 버킷(`scratch-link.aluxcoding.com` / `dev-scratch-link.aluxcoding.com`)에 업로드
3. CloudFront를 통해 `https://scratch-link.aluxcoding.com/latest.msixbundle` 같은 고정 URL로 사용자가 다운로드
4. 최신 버전 메타(`latest.json`)도 함께 갱신

이번 범위에서는 **빌드·업로드·CDN 배포까지만** 다룬다. MSIX 코드 사이닝과 `.appinstaller` 자동 업데이트는 [§9 후속 작업](#9-후속-작업-범위-밖)에 따로 기록.

## 2. 결정된 사항 요약

| 항목 | 결정 |
|---|---|
| Cloud provider | AWS (account `593793057142`, region `ap-northeast-2`) |
| Infra 프로비저닝 방식 | **aws-cli 스크립트** (Terraform/CDK 미사용) |
| 트리거 | semver git tag 푸시 |
| 채널 라우팅 | stable 태그(`v1.2.0`) → prod, prerelease 태그(`v1.2.0-*`) → dev |
| 사용자 URL | `https://scratch-link.aluxcoding.com/latest.msixbundle` (고정) |
| 버킷 네이밍 | `scratch-link.aluxcoding.com`, `dev-scratch-link.aluxcoding.com` (조직 관습 일치) |
| Origin 패턴 | S3 website endpoint + 퍼블릭 버킷 (조직 기존 28개 distribution과 동일) |
| ACM 인증서 | 기존 `*.aluxcoding.com` (us-east-1, ARN `arn:aws:acm:us-east-1:593793057142:certificate/0b58642f-b20f-451b-8257-aa366ba5fc0c`) 재사용 |
| Route 53 | 기존 `aluxcoding.com.` zone(`Z0327990CY2GCD1RONPB`)에 A/AAAA Alias 추가 |
| IAM | 전용 user `gh-actions-scratch-link` 신규 생성. 두 버킷 + 두 distribution에 한해 최소 권한 |
| GitHub Actions 인증 | 위 IAM user의 access key를 **GitHub Organization Secret**으로 등록 |
| 기존 `ci.yml` | 완전 삭제 후 신규 `release.yml` 작성 |
| 코드 사이닝 | 이번 범위 제외. 빌드는 `GenerateTemporaryStoreCertificate=True` 기본값 유지 |

## 3. AWS 인프라

### 3.1 리소스 목록 (신규 생성 6종 × 환경 2개)

각 환경(prod/dev)당 다음을 생성한다.

1. **S3 버킷** — 이름 = 도메인. `BlockPublicAccess` 해제, static website hosting 활성화, public-read 버킷 정책
2. **CloudFront distribution** — origin = S3 website endpoint (`<bucket>.s3-website.ap-northeast-2.amazonaws.com`), alias = 도메인, viewer cert = 기존 ACM, 기본 캐시 정책 (TTL은 §6에서 객체 단위 Cache-Control로 제어)
3. **Route 53 A/AAAA Alias** — `aluxcoding.com.` zone에 도메인 → CloudFront 매핑

공유 리소스 (한 번만):

4. **IAM user** `gh-actions-scratch-link` + access key + 인라인 정책 (양 환경의 권한을 한꺼번에)

### 3.2 IAM 정책 (전체)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "S3UploadProd",
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:DeleteObject", "s3:ListBucket"],
      "Resource": [
        "arn:aws:s3:::scratch-link.aluxcoding.com",
        "arn:aws:s3:::scratch-link.aluxcoding.com/*",
        "arn:aws:s3:::dev-scratch-link.aluxcoding.com",
        "arn:aws:s3:::dev-scratch-link.aluxcoding.com/*"
      ]
    },
    {
      "Sid": "CloudFrontInvalidate",
      "Effect": "Allow",
      "Action": ["cloudfront:CreateInvalidation", "cloudfront:GetInvalidation"],
      "Resource": [
        "arn:aws:cloudfront::593793057142:distribution/<PROD_DIST_ID>",
        "arn:aws:cloudfront::593793057142:distribution/<DEV_DIST_ID>"
      ]
    }
  ]
}
```

분포 ID(`PROD_DIST_ID`, `DEV_DIST_ID`)는 셋업 스크립트가 출력한 값을 정책에 반영하는 2-단계 절차로 처리한다 (§4.2).

### 3.3 OAC를 안 쓰는 이유

조직 내 기존 28개 CloudFront distribution이 모두 S3 website + 퍼블릭 버킷 패턴. 일관성을 우선해 같은 패턴 채택. OAC(Origin Access Control)로의 마이그레이션은 별도 작업으로 분리. 참고로 OAC가 더 안전한 이유는 S3 버킷을 비공개로 유지하면서 CloudFront만 접근할 수 있게 강제할 수 있기 때문.

## 4. 일회성 셋업 스크립트

### 4.1 위치 및 구성

```
scripts/aws/
  setup-cdn.sh          # 메인 entry. prod/dev 두 환경 모두 생성
  policies/
    bucket-policy.json.tpl    # public-read 정책 템플릿
    iam-policy.json.tpl       # IAM 인라인 정책 템플릿
    cloudfront-config.json.tpl # distribution 생성 입력
```

- 실행 주체: **AWS 관리자 권한이 있는 로컬 사용자** (예: 현재 `jysong`). GitHub Actions에서 실행하지 않는다 — IAM user를 만드는 일은 일회성이고 CI에 그런 권한을 주는 게 위험
- bash, aws-cli v2, `jq` 필요
- **멱등**: 이미 존재하는 리소스는 skip (각 step 전에 describe로 존재 확인)
- 실행 후 콘솔에 다음을 출력:
  - prod/dev CloudFront Distribution ID
  - prod/dev CloudFront Domain (`d*.cloudfront.net`)
  - IAM access key ID + secret (1회만 보임)
  - 다음 수동 단계 안내 ("GitHub Org Secret에 등록", "정책에 distribution ID 채우기")

### 4.2 실행 순서 (스크립트 내부)

1. S3 버킷 2개 생성 + website hosting 활성화 + public-access-block 해제 + public-read bucket policy 적용
2. CloudFront distribution 2개 생성 → ID 캡처
3. Route 53 A/AAAA Alias 2개 등록
4. IAM user 생성 + access key 발급
5. 위에서 얻은 distribution ID로 IAM 인라인 정책 렌더링 → put-user-policy
6. 결과 요약 출력

원자성 보장은 안 함(스크립트 중간 실패 시 부분 생성 상태 가능). 멱등성에 의지해서 다시 실행하면 이어서 진행됨.

## 5. GitHub Actions 워크플로

### 5.1 파일

- 기존 `.github/workflows/ci.yml` **삭제**
- 기존 `.github/workflows/signature-assistant.yml` 그대로 둠 (관련 없음)
- 기존 `.github/actions/macos-build/` **삭제**, `.github/actions/windows-build/`는 참고만 하고 새로 직접 구성
- 신규 `.github/workflows/release.yml` 작성

### 5.2 트리거

```yaml
on:
  push:
    tags:
      - 'v*.*.*'           # stable: v1.2.0
      - 'v*.*.*-*'         # prerelease: v1.2.0-beta.1, v1.2.0-rc.1
  workflow_dispatch:       # 수동 재실행 안전망
    inputs:
      channel:
        type: choice
        options: [auto, stable, dev]
        default: auto
```

채널 결정 로직(워크플로 안의 step):
- `workflow_dispatch.inputs.channel != auto` → 입력값 사용
- 태그에 `-` 포함 → `dev`
- 그 외 → `stable`

채널에 따라 다음 env가 분기:
- `BUCKET` = `scratch-link.aluxcoding.com` or `dev-scratch-link.aluxcoding.com`
- `DISTRIBUTION_ID` = (Org Secret에서 채널별 ID)
- `BASE_URL` = `https://<bucket>`

### 5.3 단일 job 구조 (`windows-latest`)

```
1. actions/checkout@v4 (fetch-depth=0, tag fetch 위해)
2. actions/setup-dotnet@v4 → 8.0.x
3. microsoft/setup-msbuild@v2
4. 채널·버전 산출 (bash step) → $GITHUB_ENV에 BUCKET, VERSION 등 export
5. msbuild scratch-link-win-msix/scratch-link-win-msix.wapproj \
     -maxCpuCount -restore -t:Build \
     -p:SolutionDir="$PWD\\" \
     -p:Configuration=Release_Win \
     -p:AppxBundlePlatforms="x86|x64|ARM64" \
     -p:AppxBundle=Always \
     -p:UapAppxPackageBuildMode=StoreAndSideload
6. 산출물 정리 (pwsh):
     Artifacts/Scratch-Link-<ver>.msixbundle
     Artifacts/Scratch-Link-<ver>.msixupload
     Artifacts/SHA256SUMS.txt
     Artifacts/latest.json
7. aws-actions/configure-aws-credentials@v4 (org secret access key)
8. aws s3 cp × 2:
     ① Artifacts/ → s3://$BUCKET/archive/v<ver>/  (Cache-Control: max-age=31536000, immutable)
     ② Scratch-Link-<ver>.msixbundle → s3://$BUCKET/latest.msixbundle
        latest.json                   → s3://$BUCKET/latest.json
        (Cache-Control: max-age=300, public)
9. aws cloudfront create-invalidation --distribution-id $DISTRIBUTION_ID --paths "/latest.msixbundle" "/latest.json"
```

> GitHub Release 생성(`gh release create`)은 본 범위에서 제외한다. 필요해지면 step 10으로 추가하는 변경이 작음.

### 5.4 사용할 GitHub Secrets

Organization 수준에서 다음 secret을 본 리포지토리에 노출시킨다.

| Secret 이름 | 값 |
|---|---|
| `AWS_ACCESS_KEY_ID` | 셋업 스크립트가 출력한 ID |
| `AWS_SECRET_ACCESS_KEY` | 셋업 스크립트가 출력한 secret |
| `AWS_REGION` | `ap-northeast-2` |
| `CF_DIST_ID_PROD` | 셋업 스크립트가 출력한 prod distribution ID |
| `CF_DIST_ID_DEV` | 셋업 스크립트가 출력한 dev distribution ID |

> **주의**: Org 시크릿에 이미 등록된 기존 `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`가 다른 리포에서 사용 중이면 키 이름 충돌이 생길 수 있다. 충돌 시 본 워크플로에서는 `SCRATCH_LINK_AWS_*` 접두사로 분리한다.

## 6. 버킷 객체 레이아웃 & 캐시 정책

```
<bucket>/
  latest.msixbundle       Cache-Control: public, max-age=300        ← 5분 캐시 (안전 마진)
  latest.json             Cache-Control: public, max-age=300
  archive/
    v1.2.0/
      Scratch-Link-1.2.0.msixbundle    Cache-Control: public, max-age=31536000, immutable
      Scratch-Link-1.2.0.msixupload    (Store 업로드용 보관, 같은 캐시)
      SHA256SUMS.txt
    v1.2.0-beta.1/
      ...
```

- 사용자가 보는 `latest.*`는 매 릴리스마다 덮어쓰기 → CloudFront invalidation 필수
- `archive/v<ver>/...`는 한 번 올리면 영원히 안 변함 → immutable, invalidation 불필요
- 두 객체만 invalidate (월 1000건 무료 한도 안에서 충분)

### 6.1 `latest.json` 스키마

```json
{
  "version": "1.2.0",
  "publishedAt": "2026-05-26T07:13:42Z",
  "channel": "stable",
  "url": "https://scratch-link.aluxcoding.com/archive/v1.2.0/Scratch-Link-1.2.0.msixbundle",
  "sha256": "f2c4...",
  "size": 18234567,
  "minWindowsBuild": 17763,
  "windowsAppRuntime": "1.8"
}
```

용도:
- 다운로드 사이트에서 최신 버전 표시 및 라벨링
- 추후 인앱 자동 업데이트 검사에 동일 스키마 재사용

`url`은 매 릴리스마다 archive 경로를 가리키도록 갱신 — `latest.msixbundle`(고정 URL)과 둘 다 제공. 고정 URL은 단순 다운로드 버튼용, archive URL은 버전 명시가 필요한 곳용.

## 7. 빌드 머신 환경

`windows-latest` 러너에 다음이 사전 설치되어 있음을 전제로 한다.

- .NET 8 SDK
- Windows SDK 10.0.22621
- MSBuild (Visual Studio 2022 Enterprise 워크로드 포함)
- `aws` CLI v2

확인 step을 워크플로 초입에 넣어 누락 시 조기 실패시킨다 (`dotnet --version`, `msbuild -version`).

## 8. 변경되는 파일 목록

```
신규:
  .github/workflows/release.yml
  scripts/aws/setup-cdn.sh
  scripts/aws/policies/bucket-policy.json.tpl
  scripts/aws/policies/iam-policy.json.tpl
  scripts/aws/policies/cloudfront-config.json.tpl
  scripts/aws/README.md                ← 셋업 절차·시크릿 등록·트러블슈팅
  docs/superpowers/specs/2026-05-26-cicd-s3-cloudfront-design.md (본 문서)

삭제:
  .github/workflows/ci.yml
  .github/actions/macos-build/
  .github/actions/windows-build/

변경:
  README.md                            ← 다운로드 URL 안내 한 줄 추가
```

## 9. 후속 작업 (범위 밖)

본 설계는 의도적으로 다음을 다루지 않는다. 각 항목은 별도 spec으로 분리한다.

### 9.1 MSIX 코드 사이닝

현재 빌드는 `GenerateTemporaryStoreCertificate=True`로 임시 자체서명만 함. **일반 사용자는 받은 `.msixbundle`을 설치할 수 없다** (Trusted Root에 인증서가 없으므로). 결정 필요 사항:
- 어떤 인증서를 쓸지 (EV / 일반 코드사이닝 / Store 등록)
- 인증서 자료(PFX, 암호) 보관 위치 (GitHub Secret / AWS Secrets Manager)
- 워크플로에 `signtool.exe sign` 단계 추가

이 결정 전까지 본 파이프라인의 산출물은 **내부 테스트용**으로만 유효.

### 9.2 MSIX `.appinstaller` 자동 업데이트

`.appinstaller` XML을 같이 배포하면, 사용자가 한 번 설치한 뒤로는 Windows가 자동으로 새 버전을 받는다. 코드 사이닝과 짝지어 적용해야 의미가 있다.

### 9.3 OAC 마이그레이션

조직 전체 정책으로 결정될 사안.

### 9.4 GitHub Actions OIDC 전환

현재는 IAM user + 장기 access key. 향후 OIDC로 전환하면 access key 로테이션 부담이 사라진다.

### 9.5 WAF (AWS WAFv2)

CloudFront 앞단에 WebACL 부착은 **이번 범위에서 제외**한다. 사용자가 받는 것은 정적 `.msixbundle` 파일 1개와 `latest.json` 1개뿐이라, 입력 검증·SQLi·XSS 같은 WAF 본연의 보호 대상이 없다. 비용·복잡도만 늘어남. 향후 다음 중 하나가 발생하면 재검토:
- 봇 트래픽이 비정상적으로 늘어 대역폭 비용이 문제됨
- 특정 국가에서의 다운로드를 차단해야 하는 요구사항 발생
- 인증된 다운로드(서명된 URL 등) 도입

## 10. 검증 시나리오 (구현 완료 시)

1. **prerelease 시나리오**: `git tag v0.0.1-test.1 && git push origin v0.0.1-test.1` → dev 버킷에 객체 도착 → `https://dev-scratch-link.aluxcoding.com/latest.json`이 `0.0.1-test.1` 표시 → `latest.msixbundle` 다운로드 가능
2. **stable 시나리오**: `git tag v0.0.1 && git push origin v0.0.1` → prod 버킷에 동일
3. **권한 시나리오**: IAM user로 다른 버킷(`scratch.aluxcoding.com` 등)에 `aws s3 cp` 시도 → AccessDenied 확인
4. **캐시 시나리오**: `latest.msixbundle` 갱신 후 5분 이내 새 버전이 CDN에 반영
5. **archive 영구 보관**: 한 달 후에도 `archive/v0.0.1/Scratch-Link-0.0.1.msixbundle` 접근 가능
