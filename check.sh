#!/usr/bin/env bash
# The gate before every commit: both front-ends' tests, the same ones CI runs.
#   C#  : restore, build (warnings are errors), every xUnit test, conformance included.
#   web : npm ci when node_modules is missing, then node --test (conformance, DOM, corpora).
# --full adds `npm pack --dry-run` (what npm would publish). Everything else is already fast.
set -euo pipefail
cd "$(dirname "$0")"

full=0
for arg in "$@"; do
  case "$arg" in
    --full) full=1 ;;
    -h|--help) echo "usage: ./check.sh [--full]"; exit 0 ;;
    *) echo "check.sh: unknown argument '$arg' (usage: ./check.sh [--full])" >&2; exit 2 ;;
  esac
done

echo "== dotnet test"
dotnet test D3sk.ChatUi.slnx -c Release

echo "== npm test"
cd packages/chat-ui
if [ ! -d node_modules ]; then
  npm ci --no-audit --no-fund
fi
npm test

if [ "$full" = 1 ]; then
  echo "== npm pack --dry-run"
  npm pack --dry-run
fi
echo "check: OK"
