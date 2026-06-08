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
  # `|| \`[]\``: alias 없는 다른 distribution의 null Items에서 contains() 에러를 회피.
  existing=$(aws cloudfront list-distributions \
    --query "DistributionList.Items[?contains(Aliases.Items || \`[]\`, \`$domain\`)].{Id:Id,Domain:DomainName}" \
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
