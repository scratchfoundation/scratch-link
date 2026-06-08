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
