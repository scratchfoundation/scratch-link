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
