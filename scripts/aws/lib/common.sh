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
