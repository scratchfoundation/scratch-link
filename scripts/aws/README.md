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

마지막에 출력되는 5개 값을 **Repository Secrets** (org가 아닌 본 repo 한정)에 등록:

| Secret 이름 | 출처 |
|---|---|
| `AWS_REGION` | 고정 `ap-northeast-2` |
| `AWS_ACCESS_KEY_ID` | 스크립트 출력 |
| `AWS_SECRET_ACCESS_KEY` | 스크립트 출력 (1회만 보임) |
| `CF_DIST_ID_PROD` | 스크립트 출력 |
| `CF_DIST_ID_DEV` | 스크립트 출력 |

> **왜 repo 시크릿?** `aluxrobot/scratch-link`은 `scratchfoundation/scratch-link`의 **public fork**라 org 시크릿(visibility=Private repositories)이 닿지 못한다. 본 작업의 IAM user `gh-actions-scratch-link`도 이 repo 전용이라 시크릿도 같은 스코프에 두는 게 일관적.

### 등록 방법 두 가지

**(권장) gh CLI** — 값이 셸 히스토리에 안 남게:
```bash
gh secret set AWS_REGION            --repo aluxrobot/scratch-link --body "ap-northeast-2"
gh secret set CF_DIST_ID_PROD       --repo aluxrobot/scratch-link --body "<from setup output>"
gh secret set CF_DIST_ID_DEV        --repo aluxrobot/scratch-link --body "<from setup output>"
gh secret set AWS_ACCESS_KEY_ID     --repo aluxrobot/scratch-link   # 프롬프트
gh secret set AWS_SECRET_ACCESS_KEY --repo aluxrobot/scratch-link   # 프롬프트
```

**브라우저**: https://github.com/aluxrobot/scratch-link/settings/secrets/actions → "New repository secret".

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
