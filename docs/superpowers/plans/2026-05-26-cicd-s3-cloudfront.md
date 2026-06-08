# CI/CD → S3 → CloudFront 파이프라인 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** semver git tag 푸시 한 번으로 Windows MSIX 빌드 → S3 업로드 → CloudFront 배포가 자동 수행되는 파이프라인을 만든다.

**Architecture:** AWS 인프라(S3·CloudFront·Route 53·IAM)는 `scripts/aws/setup-cdn.sh` 하나로 일회성 프로비저닝. 빌드/배포는 `.github/workflows/release.yml` 단일 워크플로에서 `windows-latest` 러너로 처리. 채널 라우팅은 태그 형태(`v1.2.0` vs `v1.2.0-*`)로 결정한다.

**Tech Stack:** AWS CLI v2, bash, jq (로컬), GitHub Actions, MSBuild + .NET 8 SDK (windows-latest 러너 내장), `aws-actions/configure-aws-credentials@v4`, `actions/checkout@v4`.

**참고 문서:** [docs/superpowers/specs/2026-05-26-cicd-s3-cloudfront-design.md](../specs/2026-05-26-cicd-s3-cloudfront-design.md)

---

## File Structure

```
NEW:
  scripts/aws/setup-cdn.sh                          # 메인 일회성 셋업 스크립트
  scripts/aws/lib/common.sh                         # 공통 유틸 함수 (logging, env vars)
  scripts/aws/lib/bucket.sh                         # S3 버킷 생성·정책 함수
  scripts/aws/lib/cloudfront.sh                     # CloudFront distribution 생성 함수
  scripts/aws/lib/route53.sh                        # Route 53 alias 생성 함수
  scripts/aws/lib/iam.sh                            # IAM user·policy·access key 함수
  scripts/aws/policies/bucket-policy.json.tpl       # 퍼블릭 read S3 버킷 정책 템플릿
  scripts/aws/policies/iam-policy.json.tpl          # gh-actions-scratch-link 인라인 정책 템플릿
  scripts/aws/policies/cloudfront-config.json.tpl   # CloudFront distribution 생성 입력 템플릿
  scripts/aws/README.md                             # 셋업 절차 runbook
  .github/workflows/release.yml                     # 빌드 + 업로드 + invalidate

DELETE:
  .github/workflows/ci.yml
  .github/actions/macos-build/                      # 디렉토리 전체
  .github/actions/windows-build/                    # 디렉토리 전체

MODIFY:
  README.md                                         # 다운로드 URL 안내 추가 (마지막 task)
```

각 파일은 한 가지 책임만 갖는다. 셋업 스크립트는 함수 단위로 `lib/`에 분리해 가독성·테스트 가능성을 확보.

---

## Task 1: 기존 CI 자산 삭제

기존 `ci.yml`이 macOS + Windows 둘 다 빌드하고 semantic-release npm 흐름에 묶여 있어 재사용보다 삭제가 깔끔. signature-assistant.yml은 별개 기능이라 유지.

**Files:**
- Delete: `.github/workflows/ci.yml`
- Delete: `.github/actions/macos-build/` (디렉토리)
- Delete: `.github/actions/windows-build/` (디렉토리)

- [ ] **Step 1.1: 삭제 전 백업 확인 (git이 추적 중인지)**

Run:
```bash
git ls-files .github/workflows/ci.yml .github/actions/
```
Expected: 세 경로 모두 출력됨 (삭제해도 git 히스토리에 남아 복구 가능 확인).

- [ ] **Step 1.2: 파일·디렉토리 삭제**

Run:
```bash
git rm .github/workflows/ci.yml
git rm -r .github/actions/macos-build
git rm -r .github/actions/windows-build
```

- [ ] **Step 1.3: 확인**

Run:
```bash
git status --short .github/
```
Expected: `D .github/workflows/ci.yml` 및 두 액션 디렉토리 하위 파일들의 `D` 출력. `.github/workflows/signature-assistant.yml`은 그대로 있어야 함.

- [ ] **Step 1.4: 커밋**

```bash
git commit -m "$(cat <<'EOF'
chore(ci): macOS·Windows 통합 워크플로 및 composite actions 제거

신규 release.yml(태그 트리거)로 대체 예정. signature-assistant.yml은 별개 기능이라 유지.
EOF
)"
```

---

## Task 2: 셋업 스크립트 디렉토리 구조와 공통 유틸 생성

`setup-cdn.sh`가 호출할 함수들을 `lib/`에 모듈 단위로 둔다. 먼저 공통 유틸부터.

**Files:**
- Create: `scripts/aws/lib/common.sh`

- [ ] **Step 2.1: 디렉토리 생성**

Run:
```bash
mkdir -p scripts/aws/lib scripts/aws/policies
```

- [ ] **Step 2.2: `lib/common.sh` 작성**

내용:
```bash
#!/usr/bin/env bash
# 공통 유틸: 로깅, 환경 변수, 사전 점검.
# 호출 측에서 `source "$(dirname "$0")/lib/common.sh"`로 사용.

# bash 4+ 필요 (associative array 등)
if (( BASH_VERSINFO[0] < 4 )); then
  echo "ERROR: bash 4 이상 필요 (현재: $BASH_VERSION). macOS는 'brew install bash' 후 /opt/homebrew/bin/bash로 실행." >&2
  exit 1
fi

set -euo pipefail

# 색상 로그
RED=$'\033[31m'; GREEN=$'\033[32m'; YELLOW=$'\033[33m'; BLUE=$'\033[34m'; RESET=$'\033[0m'
log_info()  { printf "%s[INFO]%s  %s\n"  "$BLUE"   "$RESET" "$*" >&2; }
log_warn()  { printf "%s[WARN]%s  %s\n"  "$YELLOW" "$RESET" "$*" >&2; }
log_error() { printf "%s[ERROR]%s %s\n"  "$RED"    "$RESET" "$*" >&2; }
log_ok()    { printf "%s[OK]%s    %s\n"  "$GREEN"  "$RESET" "$*" >&2; }

# 필수 외부 명령 점검
require_cmd() {
  local missing=()
  for cmd in "$@"; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
      missing+=("$cmd")
    fi
  done
  if (( ${#missing[@]} > 0 )); then
    log_error "다음 명령이 없습니다: ${missing[*]}"
    exit 1
  fi
}

# AWS 호출자 식별 출력 (실수 방지)
print_aws_identity() {
  local id_json
  id_json=$(aws sts get-caller-identity)
  local account user
  account=$(jq -r '.Account' <<<"$id_json")
  user=$(jq -r '.Arn' <<<"$id_json")
  log_info "AWS Account: $account"
  log_info "Caller:      $user"
}

# 환경(prod/dev) → 도메인·버킷명 매핑
domain_for_env() {
  case "$1" in
    prod) echo "scratch-link.aluxcoding.com" ;;
    dev)  echo "dev-scratch-link.aluxcoding.com" ;;
    *)    log_error "알 수 없는 환경: $1"; return 1 ;;
  esac
}

# 공용 상수
export AWS_REGION="${AWS_REGION:-ap-northeast-2}"
export ACM_CERT_ARN="${ACM_CERT_ARN:-arn:aws:acm:us-east-1:593793057142:certificate/0b58642f-b20f-451b-8257-aa366ba5fc0c}"
export ROUTE53_ZONE_ID="${ROUTE53_ZONE_ID:-Z0327990CY2GCD1RONPB}"
export IAM_USER_NAME="${IAM_USER_NAME:-gh-actions-scratch-link}"
```

- [ ] **Step 2.3: 실행 권한 부여**

Run:
```bash
chmod +x scripts/aws/lib/common.sh
```

- [ ] **Step 2.4: shellcheck (있으면) 점검**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck scripts/aws/lib/common.sh || echo "shellcheck 미설치 — 건너뜀"
```
Expected: shellcheck가 있으면 경고 없음, 없으면 메시지만 출력하고 종료 0.

- [ ] **Step 2.5: 커밋**

```bash
git add scripts/aws/lib/common.sh
git commit -m "chore(aws): 셋업 스크립트 공통 유틸 추가"
```

---

## Task 3: S3 버킷 정책 템플릿 작성

조직 기존 패턴과 동일한 퍼블릭 read 정책. 버킷 이름 placeholder만 치환.

**Files:**
- Create: `scripts/aws/policies/bucket-policy.json.tpl`

- [ ] **Step 3.1: 템플릿 작성**

내용:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PublicReadGetObject",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::__BUCKET__/*"
    }
  ]
}
```

- [ ] **Step 3.2: jq로 치환 검증**

Run:
```bash
sed 's/__BUCKET__/scratch-link.aluxcoding.com/g' scripts/aws/policies/bucket-policy.json.tpl | jq .
```
Expected: JSON 파싱 OK, Resource 값이 `arn:aws:s3:::scratch-link.aluxcoding.com/*`.

- [ ] **Step 3.3: 커밋**

```bash
git add scripts/aws/policies/bucket-policy.json.tpl
git commit -m "chore(aws): S3 버킷 public-read 정책 템플릿"
```

---

## Task 4: S3 버킷 생성 함수 작성

멱등성: 이미 있으면 skip. public access block 해제, website hosting 활성화, public-read 정책 적용까지 한 번에.

**Files:**
- Create: `scripts/aws/lib/bucket.sh`

- [ ] **Step 4.1: `lib/bucket.sh` 작성**

내용:
```bash
#!/usr/bin/env bash
# S3 버킷 생성·구성 함수. setup-cdn.sh에서 source.

# 인자: $1 = 버킷명
ensure_bucket() {
  local bucket="$1"
  local region="$AWS_REGION"

  if aws s3api head-bucket --bucket "$bucket" 2>/dev/null; then
    log_info "버킷 이미 존재: $bucket — 생성 skip"
  else
    log_info "버킷 생성: $bucket (region=$region)"
    aws s3api create-bucket \
      --bucket "$bucket" \
      --region "$region" \
      --create-bucket-configuration "LocationConstraint=$region" \
      >/dev/null
    log_ok "버킷 생성됨: $bucket"
  fi

  log_info "Public Access Block 해제: $bucket"
  aws s3api put-public-access-block \
    --bucket "$bucket" \
    --public-access-block-configuration \
      "BlockPublicAcls=false,IgnorePublicAcls=false,BlockPublicPolicy=false,RestrictPublicBuckets=false"

  log_info "Public-read 정책 적용: $bucket"
  local policy_json
  policy_json=$(sed "s/__BUCKET__/$bucket/g" "$(dirname "${BASH_SOURCE[0]}")/../policies/bucket-policy.json.tpl")
  aws s3api put-bucket-policy --bucket "$bucket" --policy "$policy_json"

  log_info "Website hosting 활성화: $bucket"
  aws s3 website "s3://$bucket/" --index-document index.html --error-document error.html
  # MSIX 다운로드 용도라 index/error 페이지는 실제로 안 쓰이지만, website endpoint 활성화에 필수.

  log_ok "버킷 구성 완료: $bucket"
}
```

- [ ] **Step 4.2: shellcheck 점검**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck -x scripts/aws/lib/bucket.sh || true
```
Expected: 경고 없음 (또는 SC1091 외 없음 — common.sh source 경로 관련).

- [ ] **Step 4.3: 커밋**

```bash
git add scripts/aws/lib/bucket.sh
git commit -m "chore(aws): S3 버킷 생성·구성 함수"
```

---

## Task 5: CloudFront distribution config 템플릿 작성

S3 website endpoint를 origin으로 하는 distribution. ACM 인증서·SSL 정책 포함.

**Files:**
- Create: `scripts/aws/policies/cloudfront-config.json.tpl`

- [ ] **Step 5.1: 템플릿 작성**

내용:
```json
{
  "CallerReference": "__CALLER_REF__",
  "Comment": "__COMMENT__",
  "Aliases": {
    "Quantity": 1,
    "Items": ["__DOMAIN__"]
  },
  "DefaultRootObject": "",
  "Origins": {
    "Quantity": 1,
    "Items": [
      {
        "Id": "s3-website-origin",
        "DomainName": "__ORIGIN_DOMAIN__",
        "CustomOriginConfig": {
          "HTTPPort": 80,
          "HTTPSPort": 443,
          "OriginProtocolPolicy": "http-only",
          "OriginSslProtocols": { "Quantity": 1, "Items": ["TLSv1.2"] },
          "OriginReadTimeout": 30,
          "OriginKeepaliveTimeout": 5
        },
        "ConnectionAttempts": 3,
        "ConnectionTimeout": 10
      }
    ]
  },
  "DefaultCacheBehavior": {
    "TargetOriginId": "s3-website-origin",
    "ViewerProtocolPolicy": "redirect-to-https",
    "AllowedMethods": {
      "Quantity": 2,
      "Items": ["GET", "HEAD"],
      "CachedMethods": { "Quantity": 2, "Items": ["GET", "HEAD"] }
    },
    "Compress": true,
    "CachePolicyId": "658327ea-f89d-4fab-a63d-7e88639e58f6"
  },
  "PriceClass": "PriceClass_200",
  "Enabled": true,
  "ViewerCertificate": {
    "ACMCertificateArn": "__CERT_ARN__",
    "SSLSupportMethod": "sni-only",
    "MinimumProtocolVersion": "TLSv1.2_2021"
  },
  "HttpVersion": "http2and3",
  "IsIPV6Enabled": true
}
```

> CachePolicyId `658327ea-f89d-4fab-a63d-7e88639e58f6`은 AWS 관리형 "CachingOptimized" 정책 ID (전 리전 공통, 고정값).
> PriceClass_200은 한국·일본·미국·유럽 포함, 남미·아프리카 제외 (비용 절감).

- [ ] **Step 5.2: jq 파싱 검증**

Run:
```bash
sed -e 's/__CALLER_REF__/test-1/g' \
    -e 's/__COMMENT__/test/g' \
    -e 's/__DOMAIN__/test.example.com/g' \
    -e 's/__ORIGIN_DOMAIN__/test.example.com.s3-website.ap-northeast-2.amazonaws.com/g' \
    -e 's|__CERT_ARN__|arn:aws:acm:us-east-1:1:certificate/x|g' \
    scripts/aws/policies/cloudfront-config.json.tpl | jq .
```
Expected: JSON 파싱 OK.

- [ ] **Step 5.3: 커밋**

```bash
git add scripts/aws/policies/cloudfront-config.json.tpl
git commit -m "chore(aws): CloudFront distribution 생성 입력 템플릿"
```

---

## Task 6: CloudFront distribution 생성 함수 작성

멱등성: 같은 alias로 이미 존재하면 ID만 반환. distribution ID와 도메인을 stdout으로 출력해 호출 측이 캡처.

**Files:**
- Create: `scripts/aws/lib/cloudfront.sh`

- [ ] **Step 6.1: `lib/cloudfront.sh` 작성**

내용:
```bash
#!/usr/bin/env bash
# CloudFront distribution 생성. setup-cdn.sh에서 source.

# 인자: $1 = 도메인 (== alias, == 버킷명)
#       $2 = comment (UI에서 식별용)
# 출력: stdout 한 줄. "DIST_ID\tCF_DOMAIN" (tab 구분)
ensure_distribution() {
  local domain="$1"
  local comment="$2"
  local origin_domain="${domain}.s3-website.${AWS_REGION}.amazonaws.com"

  local existing
  existing=$(aws cloudfront list-distributions \
    --query "DistributionList.Items[?contains(Aliases.Items, \`$domain\`)].{Id:Id,Domain:DomainName}" \
    --output json)

  local count
  count=$(jq 'length' <<<"$existing")

  if [[ "$count" -ge 1 ]]; then
    local dist_id dist_domain
    dist_id=$(jq -r '.[0].Id' <<<"$existing")
    dist_domain=$(jq -r '.[0].Domain' <<<"$existing")
    log_info "CloudFront 이미 존재 ($domain): $dist_id" >&2
    printf "%s\t%s\n" "$dist_id" "$dist_domain"
    return 0
  fi

  log_info "CloudFront distribution 생성: $domain"
  local tpl="$(dirname "${BASH_SOURCE[0]}")/../policies/cloudfront-config.json.tpl"
  local caller_ref
  caller_ref="scratch-link-$(date +%s)-$$"

  local config_json
  config_json=$(sed \
    -e "s/__CALLER_REF__/$caller_ref/g" \
    -e "s|__COMMENT__|$comment|g" \
    -e "s/__DOMAIN__/$domain/g" \
    -e "s/__ORIGIN_DOMAIN__/$origin_domain/g" \
    -e "s|__CERT_ARN__|$ACM_CERT_ARN|g" \
    "$tpl")

  local result
  result=$(aws cloudfront create-distribution \
    --distribution-config "$config_json" \
    --output json)

  local dist_id dist_domain
  dist_id=$(jq -r '.Distribution.Id' <<<"$result")
  dist_domain=$(jq -r '.Distribution.DomainName' <<<"$result")

  log_ok "CloudFront 생성됨 ($domain): $dist_id / $dist_domain" >&2
  log_warn "Distribution 배포(In-Progress → Deployed)에는 5~15분 소요." >&2

  printf "%s\t%s\n" "$dist_id" "$dist_domain"
}
```

- [ ] **Step 6.2: shellcheck**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck -x scripts/aws/lib/cloudfront.sh || true
```

- [ ] **Step 6.3: 커밋**

```bash
git add scripts/aws/lib/cloudfront.sh
git commit -m "chore(aws): CloudFront distribution 생성 함수"
```

---

## Task 7: Route 53 alias 생성 함수 작성

기존 zone `aluxcoding.com.`에 A/AAAA Alias 추가. 멱등성: 같은 이름이 같은 타겟이면 skip.

**Files:**
- Create: `scripts/aws/lib/route53.sh`

- [ ] **Step 7.1: `lib/route53.sh` 작성**

내용:
```bash
#!/usr/bin/env bash
# Route 53 A/AAAA alias 생성. setup-cdn.sh에서 source.

# 인자: $1 = 도메인 (예: scratch-link.aluxcoding.com)
#       $2 = CloudFront 도메인 (예: d123abc.cloudfront.net)
ensure_route53_alias() {
  local domain="$1"
  local cf_domain="$2"
  # CloudFront는 hosted zone ID가 전 리전 동일: Z2FDTNDATAQYW2
  local cf_zone="Z2FDTNDATAQYW2"

  # 기존 레코드 확인
  local existing
  existing=$(aws route53 list-resource-record-sets \
    --hosted-zone-id "$ROUTE53_ZONE_ID" \
    --query "ResourceRecordSets[?Name=='${domain}.' && (Type=='A' || Type=='AAAA')]" \
    --output json)

  local existing_count
  existing_count=$(jq 'length' <<<"$existing")

  if [[ "$existing_count" -ge 2 ]]; then
    # A + AAAA 둘 다 있고, target이 일치하면 skip
    local existing_target
    existing_target=$(jq -r '.[0].AliasTarget.DNSName // ""' <<<"$existing")
    if [[ "$existing_target" == "${cf_domain}." ]]; then
      log_info "Route 53 alias 이미 존재 ($domain → $cf_domain) — skip"
      return 0
    fi
    log_warn "Route 53 alias가 다른 타겟을 가리키고 있음 ($existing_target). 덮어쓰기 진행."
  fi

  log_info "Route 53 A/AAAA alias 추가: $domain → $cf_domain"

  local change_batch
  change_batch=$(cat <<EOF
{
  "Comment": "Alias for scratch-link CDN",
  "Changes": [
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "${domain}.",
        "Type": "A",
        "AliasTarget": {
          "HostedZoneId": "${cf_zone}",
          "DNSName": "${cf_domain}.",
          "EvaluateTargetHealth": false
        }
      }
    },
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "${domain}.",
        "Type": "AAAA",
        "AliasTarget": {
          "HostedZoneId": "${cf_zone}",
          "DNSName": "${cf_domain}.",
          "EvaluateTargetHealth": false
        }
      }
    }
  ]
}
EOF
)

  aws route53 change-resource-record-sets \
    --hosted-zone-id "$ROUTE53_ZONE_ID" \
    --change-batch "$change_batch" >/dev/null

  log_ok "Route 53 alias 등록됨 ($domain)"
}
```

- [ ] **Step 7.2: shellcheck**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck -x scripts/aws/lib/route53.sh || true
```

- [ ] **Step 7.3: 커밋**

```bash
git add scripts/aws/lib/route53.sh
git commit -m "chore(aws): Route 53 alias 생성 함수"
```

---

## Task 8: IAM 인라인 정책 템플릿 작성

두 버킷 + 두 distribution에 한해 최소 권한.

**Files:**
- Create: `scripts/aws/policies/iam-policy.json.tpl`

- [ ] **Step 8.1: 템플릿 작성**

내용:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "S3Upload",
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
        "arn:aws:cloudfront::__ACCOUNT__:distribution/__PROD_DIST_ID__",
        "arn:aws:cloudfront::__ACCOUNT__:distribution/__DEV_DIST_ID__"
      ]
    }
  ]
}
```

- [ ] **Step 8.2: 커밋**

```bash
git add scripts/aws/policies/iam-policy.json.tpl
git commit -m "chore(aws): gh-actions-scratch-link IAM 정책 템플릿"
```

---

## Task 9: IAM user/policy/access key 함수 작성

멱등성: user 있으면 skip, policy는 항상 put (idempotent), access key는 발급 후 1회만 출력.

**Files:**
- Create: `scripts/aws/lib/iam.sh`

- [ ] **Step 9.1: `lib/iam.sh` 작성**

내용:
```bash
#!/usr/bin/env bash
# IAM user·policy·access key 함수. setup-cdn.sh에서 source.

# IAM user 생성 (멱등). 출력 없음.
ensure_iam_user() {
  if aws iam get-user --user-name "$IAM_USER_NAME" >/dev/null 2>&1; then
    log_info "IAM user 이미 존재: $IAM_USER_NAME — skip"
    return 0
  fi
  log_info "IAM user 생성: $IAM_USER_NAME"
  aws iam create-user --user-name "$IAM_USER_NAME" >/dev/null
  log_ok "IAM user 생성됨: $IAM_USER_NAME"
}

# 인라인 정책 attach (idempotent — put-user-policy는 덮어쓰기).
# 인자: $1 = AWS Account ID, $2 = PROD_DIST_ID, $3 = DEV_DIST_ID
attach_iam_policy() {
  local account="$1"
  local prod_id="$2"
  local dev_id="$3"
  local tpl="$(dirname "${BASH_SOURCE[0]}")/../policies/iam-policy.json.tpl"

  log_info "IAM 인라인 정책 attach (gh-actions-scratch-link 정책)"
  local policy_json
  policy_json=$(sed \
    -e "s/__ACCOUNT__/$account/g" \
    -e "s/__PROD_DIST_ID__/$prod_id/g" \
    -e "s/__DEV_DIST_ID__/$dev_id/g" \
    "$tpl")

  aws iam put-user-policy \
    --user-name "$IAM_USER_NAME" \
    --policy-name "scratch-link-cdn-deploy" \
    --policy-document "$policy_json"
  log_ok "IAM 정책 attach 완료"
}

# access key 발급 또는 기존 키 안내.
# 기존 키가 있으면 secret을 다시 볼 수 없으므로, 사용자에게 옵션을 묻는다.
# 출력: stdout 한 줄. "ACCESS_KEY_ID\tSECRET_ACCESS_KEY" (신규 발급 시) 또는 빈 줄 (skip).
ensure_access_key() {
  local existing
  existing=$(aws iam list-access-keys --user-name "$IAM_USER_NAME" \
    --query 'AccessKeyMetadata[].AccessKeyId' --output json)
  local count
  count=$(jq 'length' <<<"$existing")

  if [[ "$count" -ge 1 ]]; then
    log_warn "IAM user에 access key가 이미 ${count}개 있음: $(jq -r 'join(", ")' <<<"$existing")"
    log_warn "기존 키의 secret은 재조회 불가. 분실 시 'aws iam delete-access-key' 후 재실행."
    log_warn "신규 키 발급 원하면 환경변수 FORCE_NEW_KEY=1로 재실행 (단, IAM 한도 2개)."
    if [[ "${FORCE_NEW_KEY:-}" != "1" ]]; then
      printf "\n"
      return 0
    fi
  fi

  log_info "access key 발급: $IAM_USER_NAME"
  local result
  result=$(aws iam create-access-key --user-name "$IAM_USER_NAME" --output json)
  local key_id secret
  key_id=$(jq -r '.AccessKey.AccessKeyId' <<<"$result")
  secret=$(jq -r '.AccessKey.SecretAccessKey' <<<"$result")
  log_ok "신규 access key 발급됨: $key_id"
  printf "%s\t%s\n" "$key_id" "$secret"
}
```

- [ ] **Step 9.2: shellcheck**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck -x scripts/aws/lib/iam.sh || true
```

- [ ] **Step 9.3: 커밋**

```bash
git add scripts/aws/lib/iam.sh
git commit -m "chore(aws): IAM user·정책·access key 함수"
```

---

## Task 10: 메인 셋업 스크립트 작성

`lib/*.sh`를 조립해 prod + dev 양쪽을 한 번에 프로비저닝하고 secret 등록용 값을 마지막에 출력.

**Files:**
- Create: `scripts/aws/setup-cdn.sh`

- [ ] **Step 10.1: `setup-cdn.sh` 작성**

내용:
```bash
#!/usr/bin/env bash
# Scratch Link CDN AWS 인프라 일회성 셋업.
# 멱등: 다시 실행해도 안전. 자세한 절차는 scripts/aws/README.md 참고.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"
# shellcheck source=lib/bucket.sh
source "$SCRIPT_DIR/lib/bucket.sh"
# shellcheck source=lib/cloudfront.sh
source "$SCRIPT_DIR/lib/cloudfront.sh"
# shellcheck source=lib/route53.sh
source "$SCRIPT_DIR/lib/route53.sh"
# shellcheck source=lib/iam.sh
source "$SCRIPT_DIR/lib/iam.sh"

require_cmd aws jq sed
print_aws_identity

read -r -p "위 계정·사용자로 진행할까요? (y/N) " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
  log_info "취소"
  exit 0
fi

# 1. S3 버킷
for env in prod dev; do
  domain=$(domain_for_env "$env")
  ensure_bucket "$domain"
done

# 2. CloudFront distribution
declare -A DIST_ID DIST_DOMAIN
for env in prod dev; do
  domain=$(domain_for_env "$env")
  comment="scratch-link CDN ($env)"
  result=$(ensure_distribution "$domain" "$comment")
  DIST_ID[$env]=$(cut -f1 <<<"$result")
  DIST_DOMAIN[$env]=$(cut -f2 <<<"$result")
done

# 3. Route 53 alias
for env in prod dev; do
  domain=$(domain_for_env "$env")
  ensure_route53_alias "$domain" "${DIST_DOMAIN[$env]}"
done

# 4. IAM user
ensure_iam_user
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
attach_iam_policy "$ACCOUNT_ID" "${DIST_ID[prod]}" "${DIST_ID[dev]}"

# 5. access key
key_line=$(ensure_access_key)

# 6. 요약 출력 (사용자가 GitHub Secret으로 옮길 정보)
cat >&2 <<EOF

==========================================================
${GREEN}셋업 완료. 다음 값을 GitHub Organization Secret에 등록:${RESET}

  AWS_REGION              = $AWS_REGION
  CF_DIST_ID_PROD         = ${DIST_ID[prod]}
  CF_DIST_ID_DEV          = ${DIST_ID[dev]}
EOF

if [[ -n "$key_line" ]]; then
  key_id=$(cut -f1 <<<"$key_line")
  secret=$(cut -f2 <<<"$key_line")
  cat >&2 <<EOF
  AWS_ACCESS_KEY_ID       = $key_id
  AWS_SECRET_ACCESS_KEY   = $secret
  ${YELLOW}(secret은 다시 볼 수 없으니 지금 옮기세요.)${RESET}
EOF
else
  cat >&2 <<EOF
  AWS_ACCESS_KEY_ID       = (기존 키 유지 — list-access-keys로 ID 확인)
  AWS_SECRET_ACCESS_KEY   = (이미 다른 곳에 보관된 secret 사용)
EOF
fi

cat >&2 <<EOF

CloudFront 도메인 (DNS 전파 후 사용 가능):
  prod: ${DIST_DOMAIN[prod]}  →  https://$(domain_for_env prod)
  dev:  ${DIST_DOMAIN[dev]}   →  https://$(domain_for_env dev)

다음 단계:
  1. GitHub Org Settings → Secrets and variables → Actions → New organization secret
  2. 위 5개 값 등록 (또는 기존 AWS_ACCESS_KEY_ID 재사용)
  3. 본 리포지토리에 secret 접근 허용
  4. (대기) CloudFront distribution Deployed 상태 확인:
     aws cloudfront get-distribution --id ${DIST_ID[prod]} --query 'Distribution.Status'
==========================================================
EOF
```

- [ ] **Step 10.2: 실행 권한 부여**

Run:
```bash
chmod +x scripts/aws/setup-cdn.sh
```

- [ ] **Step 10.3: shellcheck**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck -x scripts/aws/setup-cdn.sh || true
```

- [ ] **Step 10.4: dry-run (구문 확인 정도)**

Run:
```bash
bash -n scripts/aws/setup-cdn.sh && echo "syntax OK"
```
Expected: `syntax OK`.

- [ ] **Step 10.5: 커밋**

```bash
git add scripts/aws/setup-cdn.sh
git commit -m "feat(aws): scratch-link CDN 일회성 셋업 스크립트"
```

---

## Task 11: 셋업 runbook 작성

스크립트 실행 절차·전제조건·시크릿 등록·트러블슈팅을 한 문서에.

**Files:**
- Create: `scripts/aws/README.md`

- [ ] **Step 11.1: README 작성**

내용:
```markdown
# Scratch Link CDN AWS Setup

`scratch-link.aluxcoding.com` (prod) / `dev-scratch-link.aluxcoding.com` (dev) 두 환경의 S3·CloudFront·Route 53·IAM 리소스를 한 번에 프로비저닝하는 스크립트.

설계 문서: [`docs/superpowers/specs/2026-05-26-cicd-s3-cloudfront-design.md`](../../docs/superpowers/specs/2026-05-26-cicd-s3-cloudfront-design.md)

## 전제

- aws-cli v2, jq, bash 4+ (macOS는 `brew install bash`)
- AWS 관리자 권한 (IAM user/policy 생성 가능)
- Account `593793057142`, region `ap-northeast-2`
- ACM `*.aluxcoding.com` 인증서가 us-east-1에 존재 (이미 발급되어 있음)
- Route 53 `aluxcoding.com.` zone (이미 있음)

## 실행

```bash
cd <repo-root>
./scripts/aws/setup-cdn.sh
```

스크립트가 AWS 호출자(account, user)를 보여주고 진행 여부를 물어봅니다. 잘못된 계정이면 `n`을 입력해 취소.

## 산출

마지막에 출력되는 5개 값을 GitHub **Organization** Secrets에 등록:

| Secret 이름 | 출처 |
|---|---|
| `AWS_REGION` | 고정 `ap-northeast-2` |
| `AWS_ACCESS_KEY_ID` | 스크립트 출력 |
| `AWS_SECRET_ACCESS_KEY` | 스크립트 출력 (1회만 보임) |
| `CF_DIST_ID_PROD` | 스크립트 출력 |
| `CF_DIST_ID_DEV` | 스크립트 출력 |

등록 경로: GitHub Org → Settings → Secrets and variables → Actions → New organization secret. 본 리포지토리(`aluxrobot/scratch-link`)를 접근 가능 리포에 추가.

> 기존 Org Secret에 `AWS_ACCESS_KEY_ID` 이름이 이미 다른 키로 등록돼 있으면, 본 워크플로용 이름을 `SCRATCH_LINK_AWS_ACCESS_KEY_ID` 등으로 바꿔 등록하고 `release.yml`의 secret 참조도 같이 바꾸세요.

## 멱등성

- 모든 리소스는 존재 확인 후 skip
- IAM 정책은 항상 덮어쓰기 (안전)
- IAM access key는 이미 있으면 skip (secret 재조회 불가). 강제 신규 발급: `FORCE_NEW_KEY=1 ./scripts/aws/setup-cdn.sh`

## CloudFront 전파 대기

생성 직후 `In-Progress` 상태. Deployed 될 때까지 5~15분.

```bash
aws cloudfront get-distribution --id $CF_DIST_ID_PROD --query 'Distribution.Status'
```

`Deployed`가 뜨면 https://scratch-link.aluxcoding.com/ 접근 가능 (단, DNS 전파도 같이 필요. 보통 즉시 ~ 수 분).

## 롤백·재구성

생성된 리소스를 일괄 제거하는 스크립트는 만들지 않음. 필요 시:

```bash
# CloudFront disable + delete
aws cloudfront update-distribution --id <ID> --if-match <ETAG> --distribution-config <disabled-config>
aws cloudfront delete-distribution --id <ID> --if-match <ETAG>

# Route 53 레코드 삭제
aws route53 change-resource-record-sets --hosted-zone-id Z0327990CY2GCD1RONPB --change-batch <DELETE-batch>

# S3 버킷 비우고 삭제
aws s3 rm s3://scratch-link.aluxcoding.com --recursive
aws s3api delete-bucket --bucket scratch-link.aluxcoding.com

# IAM user
aws iam delete-user-policy --user-name gh-actions-scratch-link --policy-name scratch-link-cdn-deploy
aws iam delete-access-key --user-name gh-actions-scratch-link --access-key-id <KEY_ID>
aws iam delete-user --user-name gh-actions-scratch-link
```

## 트러블슈팅

| 증상 | 원인 / 조치 |
|---|---|
| `BucketAlreadyOwnedByYou` 외의 `BucketAlreadyExists` | 글로벌하게 같은 이름의 버킷이 다른 계정에서 사용 중. 도메인 이름을 바꿔야 함. |
| CloudFront 생성 시 `CNAMEAlreadyExists` | 다른 distribution이 같은 alias를 들고 있음. 기존 distribution을 disable/delete 또는 alias 회수 필요. |
| `InvalidViewerCertificate` | ACM 인증서가 us-east-1이 아니거나 ISSUED 상태가 아님. `ACM_CERT_ARN` 재확인. |
| `MalformedPolicyDocument` | IAM 정책 템플릿의 placeholder 미치환. `setup-cdn.sh`가 sed 치환을 정상 수행했는지 stderr 로그 확인. |
```

- [ ] **Step 11.2: 커밋**

```bash
git add scripts/aws/README.md
git commit -m "docs(aws): CDN 셋업 runbook"
```

---

## Task 12: 셋업 스크립트 로컬 실행 (수동, 일회성)

여기까지가 인프라. 이제 실제로 AWS에 리소스를 만든다. 비용 발생 — CloudFront, Route53 호스팅 비용은 호출당. 진행 전 확인할 것.

- [ ] **Step 12.1: AWS 호출자 검증**

Run:
```bash
aws sts get-caller-identity
```
Expected: Account `593793057142`, user `jysong` (또는 동등 관리자 권한 user).

- [ ] **Step 12.2: 스크립트 실행**

Run:
```bash
./scripts/aws/setup-cdn.sh
```
Expected: 마지막 요약 박스에 5개 값 출력. 중간 단계에서 `[OK]` 로그 다수.

- [ ] **Step 12.3: 결과 검증 — S3**

Run:
```bash
aws s3api head-bucket --bucket scratch-link.aluxcoding.com && echo OK
aws s3api head-bucket --bucket dev-scratch-link.aluxcoding.com && echo OK
aws s3api get-bucket-website --bucket scratch-link.aluxcoding.com
```
Expected: 두 버킷 OK 출력, website 설정에 `IndexDocument` 존재.

- [ ] **Step 12.4: 결과 검증 — CloudFront**

Run:
```bash
aws cloudfront list-distributions \
  --query 'DistributionList.Items[?contains(Aliases.Items, `scratch-link.aluxcoding.com`)].{Id:Id,Status:Status,Domain:DomainName}' \
  --output table
```
Expected: 1개 행, Status는 `Deployed` 또는 `InProgress`.

- [ ] **Step 12.5: 결과 검증 — Route 53**

Run:
```bash
aws route53 list-resource-record-sets --hosted-zone-id Z0327990CY2GCD1RONPB \
  --query "ResourceRecordSets[?starts_with(Name, 'scratch-link.aluxcoding.com') || starts_with(Name, 'dev-scratch-link.aluxcoding.com')]" \
  --output json | jq -r '.[] | "\(.Name) \(.Type) -> \(.AliasTarget.DNSName // "n/a")"'
```
Expected: 4줄 (A/AAAA × 2 도메인).

- [ ] **Step 12.6: 결과 검증 — IAM**

Run:
```bash
aws iam get-user --user-name gh-actions-scratch-link --query 'User.Arn'
aws iam get-user-policy --user-name gh-actions-scratch-link --policy-name scratch-link-cdn-deploy \
  --query 'PolicyDocument' | jq .
```
Expected: ARN 출력 + 정책 JSON에 distribution ID가 채워져 있음 (placeholder 없음).

- [ ] **Step 12.7: GitHub Org Secret 등록 (수동, 브라우저)**

GitHub Org Settings → Secrets and variables → Actions → New organization secret 에서 다음 5개 등록:
- `AWS_REGION`
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `CF_DIST_ID_PROD`
- `CF_DIST_ID_DEV`

각 secret에 `aluxrobot/scratch-link` 리포 접근 허용.

> 이 단계는 코드 변경 없음. 완료 후 다음 task로.

---

## Task 13: release 워크플로 스켈레톤 작성 (트리거 + 채널 결정)

빌드·업로드 step은 다음 task에서 채우고, 먼저 트리거와 채널 결정 로직을 검증 가능한 형태로 만든다.

**Files:**
- Create: `.github/workflows/release.yml`

- [ ] **Step 13.1: 워크플로 스켈레톤 작성**

내용:
```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'
      - 'v*.*.*-*'
  workflow_dispatch:
    inputs:
      channel:
        description: 'Override channel (auto/stable/dev). auto=태그 형식으로 결정'
        type: choice
        options: [auto, stable, dev]
        default: auto
      ref:
        description: '수동 실행 시 사용할 ref (태그/브랜치). 비우면 워크플로 ref 사용'
        type: string
        default: ''

concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false

permissions:
  contents: read

jobs:
  release:
    runs-on: windows-latest
    defaults:
      run:
        shell: bash
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: ${{ github.event.inputs.ref || github.ref }}

      - name: Resolve channel and version
        id: meta
        run: |
          set -euo pipefail
          # 채널 결정
          input="${{ github.event.inputs.channel }}"
          tag_name="${GITHUB_REF_NAME:-}"
          if [[ "$input" == "stable" || "$input" == "dev" ]]; then
            channel="$input"
          elif [[ "$tag_name" =~ ^v[0-9]+\.[0-9]+\.[0-9]+- ]]; then
            channel="dev"
          elif [[ "$tag_name" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            channel="stable"
          else
            channel="dev"  # workflow_dispatch + auto + non-tag ref → 안전한 쪽
          fi
          # 버전 산출 (태그면 v 제거, 아니면 git describe)
          if [[ "$tag_name" =~ ^v ]]; then
            version="${tag_name#v}"
          else
            version="$(git describe --tags --always --dirty)"
          fi
          # 채널별 버킷·distribution
          if [[ "$channel" == "stable" ]]; then
            bucket="scratch-link.aluxcoding.com"
            dist_id="${{ secrets.CF_DIST_ID_PROD }}"
          else
            bucket="dev-scratch-link.aluxcoding.com"
            dist_id="${{ secrets.CF_DIST_ID_DEV }}"
          fi
          {
            echo "channel=$channel"
            echo "version=$version"
            echo "bucket=$bucket"
            echo "dist_id=$dist_id"
          } | tee -a "$GITHUB_OUTPUT"

      - name: Show resolved metadata
        run: |
          echo "channel = ${{ steps.meta.outputs.channel }}"
          echo "version = ${{ steps.meta.outputs.version }}"
          echo "bucket  = ${{ steps.meta.outputs.bucket }}"
          echo "dist_id = (마스킹됨)"
```

> 빌드/업로드 step은 다음 task에서 추가. 지금은 채널 결정만 검증.

- [ ] **Step 13.2: 워크플로 YAML 구문 검증**

Run:
```bash
python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml')); print('OK')"
```
Expected: `OK`. (yaml 모듈 없으면 `pip3 install pyyaml`)

- [ ] **Step 13.3: 커밋**

```bash
git add .github/workflows/release.yml
git commit -m "feat(ci): release.yml 트리거·채널 결정 스켈레톤"
```

- [ ] **Step 13.4: 브랜치 푸시 + workflow_dispatch로 검증 (인프라 변경 없는 dry-run)**

Run:
```bash
git push -u origin "$(git branch --show-current)"
```

GitHub UI → Actions → Release → Run workflow → channel=`dev`, ref=현재 브랜치명 → 실행.

Expected: 워크플로가 성공 종료. `Show resolved metadata` step 로그에 `channel = dev`, `version = ...`, `bucket = dev-scratch-link.aluxcoding.com` 출력.

> 빌드 step이 아직 없어서 이 단계는 채널 결정만 검증.

---

## Task 14: 빌드 step 추가 (.NET + msbuild + MSIX)

기존 `windows-build` 액션의 msbuild 호출을 참고해 step 단위로 풀어쓴다.

**Files:**
- Modify: `.github/workflows/release.yml`

- [ ] **Step 14.1: meta step 뒤에 빌드 step 추가**

`Show resolved metadata` step 뒤에 다음 step들을 추가:

```yaml
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Setup MSBuild
        uses: microsoft/setup-msbuild@v2

      - name: Verify build toolchain
        shell: pwsh
        run: |
          dotnet --version
          msbuild -version
          aws --version

      - name: Restore NuGet cache
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/packages.lock.json', '**/*.csproj', '**/*.props') }}
          restore-keys: ${{ runner.os }}-nuget-

      - name: Build MSIX bundle
        shell: pwsh
        env:
          SOLUTION_DIR: ${{ github.workspace }}
        run: |
          msbuild scratch-link-win-msix/scratch-link-win-msix.wapproj `
            -maxCpuCount `
            -restore `
            -t:Build `
            -p:SolutionDir="$env:SOLUTION_DIR\" `
            -p:Configuration=Release_Win `
            -p:AppxBundlePlatforms="x86|x64|ARM64" `
            -p:AppxBundle=Always `
            -p:UapAppxPackageBuildMode=StoreAndSideload
```

- [ ] **Step 14.2: YAML 구문 검증**

Run:
```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml')); print('OK')"
```

- [ ] **Step 14.3: 커밋**

```bash
git add .github/workflows/release.yml
git commit -m "feat(ci): release.yml MSIX 빌드 step 추가"
```

- [ ] **Step 14.4: workflow_dispatch로 빌드 검증**

GitHub UI → Run workflow (channel=dev, ref=현재 브랜치).

Expected: `Build MSIX bundle` step이 성공. 로그 끝쪽에 `scratch-link-win-msix\AppPackages\...msixbundle` 생성 메시지.

> 실패 시 보통 다음 중 하나:
> - WindowsAppSDK 1.8 NuGet 복원 실패 → restore step 로그 확인
> - StyleCop 경고가 error로 승격 → 코드 문제이므로 워크플로 외 해결
> - `Package.appxmanifest` 버전 주입 오류 → ScratchVersion.targets 동작 확인

---

## Task 15: 산출물 정리 + latest.json 생성 step 추가

빌드가 만든 패키지를 `Artifacts/`로 옮기고 `SHA256SUMS.txt` + `latest.json`을 생성.

**Files:**
- Modify: `.github/workflows/release.yml`

- [ ] **Step 15.1: 빌드 step 뒤에 정리 step 추가**

`Build MSIX bundle` 뒤에:

```yaml
      - name: Collect artifacts
        shell: bash
        run: |
          set -euo pipefail
          mkdir -p Artifacts

          version="${{ steps.meta.outputs.version }}"
          channel="${{ steps.meta.outputs.channel }}"
          bundle_src=$(find scratch-link-win-msix/AppPackages -type f -name '*.msixbundle' | head -n1)
          upload_src=$(find scratch-link-win-msix/AppPackages -type f -name '*.msixupload' | head -n1)

          if [[ -z "$bundle_src" || -z "$upload_src" ]]; then
            echo "::error::빌드 산출물(.msixbundle / .msixupload)을 찾지 못함"
            ls -R scratch-link-win-msix/AppPackages || true
            exit 1
          fi

          bundle_dst="Artifacts/Scratch-Link-${version}.msixbundle"
          upload_dst="Artifacts/Scratch-Link-${version}.msixupload"
          cp "$bundle_src" "$bundle_dst"
          cp "$upload_src" "$upload_dst"

          # 체크섬
          (cd Artifacts && sha256sum "$(basename "$bundle_dst")" "$(basename "$upload_dst")") > Artifacts/SHA256SUMS.txt

          # latest.json
          sha=$(sha256sum "$bundle_dst" | awk '{print $1}')
          size=$(stat -c %s "$bundle_dst" 2>/dev/null || stat -f %z "$bundle_dst")
          published=$(date -u +%Y-%m-%dT%H:%M:%SZ)
          archive_url="https://${{ steps.meta.outputs.bucket }}/archive/v${version}/$(basename "$bundle_dst")"

          jq -n \
            --arg version "$version" \
            --arg publishedAt "$published" \
            --arg channel "$channel" \
            --arg url "$archive_url" \
            --arg sha256 "$sha" \
            --argjson size "$size" \
            '{version:$version, publishedAt:$publishedAt, channel:$channel, url:$url, sha256:$sha256, size:$size, minWindowsBuild:17763, windowsAppRuntime:"1.8"}' \
            > Artifacts/latest.json

          ls -la Artifacts/
          cat Artifacts/latest.json
```

- [ ] **Step 15.2: YAML 구문 검증**

Run:
```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml')); print('OK')"
```

- [ ] **Step 15.3: 커밋**

```bash
git add .github/workflows/release.yml
git commit -m "feat(ci): MSIX 산출물 정리 및 latest.json 생성"
```

- [ ] **Step 15.4: workflow_dispatch 검증**

Run workflow. Expected: `Collect artifacts` step 로그 끝에 `latest.json` 내용 출력. version, channel, url, sha256, size가 채워져 있음.

---

## Task 16: S3 업로드 + CloudFront invalidation step 추가

archive 경로(immutable) + `latest.*`(짧은 캐시) 두 흐름.

**Files:**
- Modify: `.github/workflows/release.yml`

- [ ] **Step 16.1: Collect artifacts 뒤에 업로드 step 추가**

```yaml
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ secrets.AWS_REGION }}

      - name: Upload archive (immutable)
        shell: bash
        run: |
          set -euo pipefail
          version="${{ steps.meta.outputs.version }}"
          bucket="${{ steps.meta.outputs.bucket }}"
          aws s3 cp Artifacts/ "s3://${bucket}/archive/v${version}/" \
            --recursive \
            --exclude "latest.json" \
            --cache-control "public, max-age=31536000, immutable" \
            --metadata-directive REPLACE

      - name: Upload latest pointers (short cache)
        shell: bash
        run: |
          set -euo pipefail
          version="${{ steps.meta.outputs.version }}"
          bucket="${{ steps.meta.outputs.bucket }}"
          aws s3 cp "Artifacts/Scratch-Link-${version}.msixbundle" \
            "s3://${bucket}/latest.msixbundle" \
            --cache-control "public, max-age=300" \
            --content-type "application/vnd.ms-appx"
          aws s3 cp Artifacts/latest.json \
            "s3://${bucket}/latest.json" \
            --cache-control "public, max-age=300" \
            --content-type "application/json"

      - name: Invalidate CloudFront latest paths
        shell: bash
        run: |
          set -euo pipefail
          aws cloudfront create-invalidation \
            --distribution-id "${{ steps.meta.outputs.dist_id }}" \
            --paths "/latest.msixbundle" "/latest.json" \
            --query 'Invalidation.{Id:Id,Status:Status}' \
            --output table
```

- [ ] **Step 16.2: YAML 구문 검증 + 커밋**

Run:
```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml')); print('OK')"
git add .github/workflows/release.yml
git commit -m "feat(ci): S3 업로드 및 CloudFront invalidation"
```

- [ ] **Step 16.3: workflow_dispatch 전체 흐름 검증 (dev 채널)**

Run workflow with channel=`dev`, ref=현재 브랜치. (workflow_dispatch니까 태그가 아니어도 됨.)

Expected: 모든 step 성공. 마지막 invalidation step 로그에 Id + Status `InProgress`.

- [ ] **Step 16.4: dev 버킷 객체 확인**

Run (로컬):
```bash
aws s3 ls s3://dev-scratch-link.aluxcoding.com/ --recursive
```
Expected: `latest.msixbundle`, `latest.json`, `archive/v<ver>/...` 객체들.

- [ ] **Step 16.5: dev CDN에서 다운로드 확인**

Run (로컬):
```bash
curl -sIL https://dev-scratch-link.aluxcoding.com/latest.json | grep -E '^(HTTP|Cache-Control|Content-Type)'
curl -s https://dev-scratch-link.aluxcoding.com/latest.json | jq .
```
Expected: HTTP 200, `Cache-Control: public, max-age=300`, JSON 본문에 version·url 등 출력.

> CloudFront 전파가 끝났는지 + DNS가 풀렸는지가 전제. `Deployed` 상태 확인은 Task 12.4 명령.

---

## Task 17: 정식 태그로 stable 경로 검증

prerelease 태그가 아닌 stable 태그를 한 번 푸시해 prod 경로가 동작하는지 확인.

> 이 단계는 실제 release를 만드는 것이라 신중. 검증 후 필요하면 prod 버킷 객체와 태그를 정리할 수 있음.

- [ ] **Step 17.1: 검증용 prerelease 태그 푸시 (dev 자동 트리거 확인)**

Run:
```bash
git tag v0.0.1-test.1
git push origin v0.0.1-test.1
```
Expected: GitHub Actions에 `Release` 워크플로가 자동 트리거. 채널 자동 인식 → dev.

검증: `aws s3 ls s3://dev-scratch-link.aluxcoding.com/archive/v0.0.1-test.1/`.

- [ ] **Step 17.2: 검증용 stable 태그 푸시 (prod 자동 트리거 확인)**

Run:
```bash
git tag v0.0.1-rc-prod.1   # 본 태그도 prerelease 형태라 dev로 가야 함
```

위는 의도적 dev 라우팅 확인용. 실제 prod 검증은:

```bash
git tag v0.0.1
git push origin v0.0.1
```
Expected: 워크플로 자동 실행 → channel=stable → prod 버킷·distribution 사용.

검증:
```bash
aws s3 ls s3://scratch-link.aluxcoding.com/
curl -s https://scratch-link.aluxcoding.com/latest.json | jq .
```
Expected: 두 파일 + archive, latest.json의 channel=`stable`.

- [ ] **Step 17.3: 정리 — 검증 태그·객체 삭제 (선택)**

검증용 태그가 git 히스토리에 남는 게 싫으면:
```bash
git tag -d v0.0.1-test.1 v0.0.1
git push origin :v0.0.1-test.1 :v0.0.1
```
S3에서도 archive 객체 삭제:
```bash
aws s3 rm s3://dev-scratch-link.aluxcoding.com/archive/v0.0.1-test.1/ --recursive
aws s3 rm s3://scratch-link.aluxcoding.com/archive/v0.0.1/ --recursive
```

`latest.*`는 다음 진짜 릴리스가 덮어쓰므로 남겨두어도 무방.

---

## Task 18: README 다운로드 안내 추가

사용자가 어디서 받는지 README에 한 줄.

**Files:**
- Modify: `README.md`

- [ ] **Step 18.1: 다운로드 섹션 추가**

`README.md`의 "패키징 및 배포" 섹션 위에 새 섹션 삽입:

```markdown
## 다운로드

| 채널 | URL |
|---|---|
| Stable | https://scratch-link.aluxcoding.com/latest.msixbundle |
| Prerelease (개발판) | https://dev-scratch-link.aluxcoding.com/latest.msixbundle |

최신 버전 메타: `latest.json` (같은 디렉토리). 특정 버전: `archive/v<version>/...`.

> 현재 빌드는 임시 자체서명 인증서로 서명되어 있어 일반 사용자 PC에서는 설치 전 인증서를 Trusted Root에 수동 설치해야 합니다. 정식 코드사이닝은 별도 작업으로 진행 예정.

```

- [ ] **Step 18.2: 커밋**

```bash
git add README.md
git commit -m "docs: 다운로드 URL 안내 추가"
```

---

## Task 19: PR 생성

브랜치 `feature/cicd-s3-cloudfront`를 develop으로 머지.

- [ ] **Step 19.1: 브랜치 push (이미 됐으면 skip)**

Run:
```bash
git push -u origin feature/cicd-s3-cloudfront
```

- [ ] **Step 19.2: PR 생성**

Run:
```bash
gh pr create --base develop --title "feat(cicd): S3 + CloudFront 배포 파이프라인" --body "$(cat <<'EOF'
## Summary
- 기존 `ci.yml`·`macos-build`·`windows-build` 액션 제거
- AWS S3·CloudFront·Route 53·IAM 일회성 셋업 스크립트(`scripts/aws/setup-cdn.sh`) 추가
- `release.yml` 신규: semver 태그 푸시 → MSIX 빌드 → S3 업로드 → CloudFront invalidation
- `scratch-link.aluxcoding.com` (stable) / `dev-scratch-link.aluxcoding.com` (prerelease) 라우팅
- 설계 문서: `docs/superpowers/specs/2026-05-26-cicd-s3-cloudfront-design.md`
- 후속 작업(범위 외): 코드 사이닝, `.appinstaller` 자동 업데이트, OAC/OIDC 전환, WAF

## Test plan
- [x] dev 채널 workflow_dispatch 성공
- [x] dev 채널 자동 태그 트리거 성공 (`v0.0.1-test.1`)
- [x] prod 채널 자동 태그 트리거 성공 (`v0.0.1`)
- [x] CDN 다운로드 동작 확인 (`curl latest.json`)
- [x] IAM user 권한이 두 버킷·두 distribution에만 한정됨 확인
EOF
)"
```

---

## Self-Review (작성 후 확인 완료)

**Spec coverage** — spec의 모든 섹션이 task로 매핑됨:
- §3 AWS 인프라 → Task 2~10
- §4 셋업 스크립트 → Task 10, runbook은 Task 11
- §5 워크플로 → Task 13~16
- §6 버킷 레이아웃·캐시 정책 → Task 15, 16
- §7 빌드 환경 점검 → Task 14 (Verify build toolchain)
- §8 변경 파일 목록 → Task 1(삭제), Task 18(README)
- §9 후속 작업 → PR 본문에 명시
- §10 검증 시나리오 → Task 17

**Placeholder scan** — TODO/TBD/"적절히" 없음. 모든 코드는 그대로 복붙 가능.

**Type consistency** — `domain_for_env`, `ensure_bucket`, `ensure_distribution`, `ensure_route53_alias`, `ensure_iam_user`, `attach_iam_policy`, `ensure_access_key` 함수 시그니처가 setup-cdn.sh의 호출 측과 일치. 환경 변수명(`AWS_REGION`, `ACM_CERT_ARN`, `ROUTE53_ZONE_ID`, `IAM_USER_NAME`)이 common.sh 정의와 호출 측 일치. GitHub Secret 이름(`CF_DIST_ID_PROD/DEV`, `AWS_ACCESS_KEY_ID/SECRET/REGION`)이 setup 스크립트 출력 안내 → README → 워크플로 사이에 일관.

**Scope** — 단일 임플 플랜으로 적정. 19개 task, 인프라 5개 + 워크플로 4개 + 검증 3개 + 메타 7개. 각 task는 2~10분 내 완료 가능.
