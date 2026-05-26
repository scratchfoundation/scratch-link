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
