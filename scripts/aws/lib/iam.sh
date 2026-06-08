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
