#!/usr/bin/env bash
set -euo pipefail

api_base="${SMOKE_API_BASE_URL:-}"
web_base="${SMOKE_WEB_BASE_URL:-}"

if [[ -z "$api_base" || -z "$web_base" ]]; then
  echo 'SMOKE_API_BASE_URL and SMOKE_WEB_BASE_URL are required.' >&2
  exit 2
fi

api_base="${api_base%/}"
web_base="${web_base%/}"

retry_get() {
  local url="$1"
  local expected="$2"
  local attempt code
  for attempt in {1..12}; do
    code="$(curl --silent --show-error --location --output /dev/null --write-out '%{http_code}' --max-time 20 "$url" || true)"
    if [[ "$code" == "$expected" ]]; then
      echo "PASS $expected $url"
      return 0
    fi
    sleep 5
  done
  echo "FAIL expected $expected from $url; received $code" >&2
  return 1
}

retry_get "$api_base/" 200

auth_code="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' --max-time 20 "$api_base/api/quote-requests")"
if [[ "$auth_code" != '401' ]]; then
  echo "FAIL anonymous quote-request API expected 401; received $auth_code" >&2
  exit 1
fi
echo 'PASS anonymous quote-request API fails closed with 401'

for path in /bdr/public /thinkpink/public; do
  retry_get "$web_base$path" 200
done

for path in /bdr/admin/dashboard /thinkpink/admin/dashboard /turnkeyops/admin/dashboard; do
  headers="$(curl --silent --show-error --head --header 'Accept: text/html' --max-time 20 "$web_base$path")"
  if ! grep -Eq '^HTTP/[^ ]+ 30[237]' <<<"$headers" || ! grep -Eqi '^location: .*/auth/login\?returnTo=' <<<"$headers"; then
    echo "FAIL anonymous admin smoke for $path" >&2
    echo "$headers" >&2
    exit 1
  fi
  echo "PASS anonymous admin redirect $path"
done

echo 'Post-deploy smoke checks passed.'
