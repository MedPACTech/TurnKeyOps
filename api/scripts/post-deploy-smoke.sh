#!/usr/bin/env bash
set -euo pipefail

component="${SMOKE_COMPONENT:-all}"
case "$component" in api|web|all) ;; *) echo 'Invalid SMOKE_COMPONENT' >&2; exit 2 ;; esac

api_base="${SMOKE_API_BASE_URL:-}"
web_base="${SMOKE_WEB_BASE_URL:-}"

if [[ -z "$api_base" || ( "$component" != api && -z "$web_base" ) ]]; then
  echo 'SMOKE_API_BASE_URL and SMOKE_WEB_BASE_URL are required.' >&2
  exit 2
fi

api_base="${api_base%/}"
web_base="${web_base%/}"
# One wall-clock budget for the whole smoke stage, including request timeouts.
timeout_seconds="${SMOKE_TIMEOUT_SECONDS:-300}"
max_attempts="${SMOKE_MAX_ATTEMPTS:-60}"
for value in "$timeout_seconds" "$max_attempts"; do
  if ! [[ "$value" =~ ^[1-9][0-9]*$ ]]; then
    echo 'Smoke timeout and attempt count must be positive integers.' >&2
    exit 2
  fi
done
smoke_deadline=$((SECONDS + timeout_seconds))

bounded_curl() {
  local remaining=$((smoke_deadline - SECONDS))
  if (( remaining <= 0 )); then
    echo "Smoke deadline exhausted after ${timeout_seconds}s" >&2
    return 28
  fi
  local request_timeout=20
  (( remaining >= request_timeout )) || request_timeout="$remaining"
  curl --connect-timeout 5 --max-time "$request_timeout" "$@"
}

retry_get() {
  local url="$1"
  local expected="$2"
  local attempt code
  for ((attempt = 1; attempt <= max_attempts; attempt += 1)); do
    code="$(bounded_curl --silent --show-error --location --output /dev/null --write-out '%{http_code}' "$url" || true)"
    if [[ "$code" == "$expected" ]]; then
      echo "PASS $expected $url"
      return 0
    fi
    local remaining=$((smoke_deadline - SECONDS))
    if (( remaining <= 0 || attempt == max_attempts )); then break; fi
    local delay=5
    (( remaining >= delay )) || delay="$remaining"
    sleep "$delay"
  done
  echo "FAIL expected $expected from $url; received $code" >&2
  return 1
}

retry_get "$api_base/" 200

auth_code="$(bounded_curl --silent --show-error --output /dev/null --write-out '%{http_code}' "$api_base/api/quote-requests")"
if [[ "$auth_code" != '401' ]]; then
  echo "FAIL anonymous quote-request API expected 401; received $auth_code" >&2
  exit 1
fi
echo 'PASS anonymous quote-request API fails closed with 401'

if [[ "$component" == api ]]; then
  echo 'API post-deploy smoke checks passed.'
  exit 0
fi

for path in /bdr/public /thinkpink/public; do
  retry_get "$web_base$path" 200
done

for path in /bdr/admin/dashboard /thinkpink/admin/dashboard /turnkeyops/admin/dashboard; do
  headers="$(bounded_curl --silent --show-error --head --header 'Accept: text/html' "$web_base$path")"
  if ! grep -Eq '^HTTP/[^ ]+ 30[237]' <<<"$headers" || ! grep -Eqi '^location: .*/auth/login\?returnTo=' <<<"$headers"; then
    echo "FAIL anonymous admin smoke for $path" >&2
    echo "$headers" >&2
    exit 1
  fi
  echo "PASS anonymous admin redirect $path"
done

echo 'Post-deploy smoke checks passed.'
