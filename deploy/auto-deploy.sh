#!/usr/bin/env bash
# auto-deploy.sh — polled by the onvif-deploy systemd timer.
#
# If origin/master has new commits, fast-forward and rebuild the compose stack.
# Idempotent: if nothing new, exits silently. Output goes to journald via
# the systemd service unit; nothing is written to disk.

set -euo pipefail

REPO_DIR="${REPO_DIR:-$HOME/onvif-web}"
BRANCH="${BRANCH:-master}"
COMPOSE_DIR="$REPO_DIR/deploy"

cd "$REPO_DIR"

git fetch --quiet origin "$BRANCH"

LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse "origin/$BRANCH")

if [ "$LOCAL" = "$REMOTE" ]; then
    exit 0
fi

echo "[$(date -Iseconds)] new commits: ${LOCAL:0:7} -> ${REMOTE:0:7}"
git --no-pager log --oneline "${LOCAL}..${REMOTE}"

git pull --ff-only --quiet origin "$BRANCH"

cd "$COMPOSE_DIR"
docker compose up -d --build

docker image prune -f >/dev/null

echo "[$(date -Iseconds)] deploy finished at $(git rev-parse --short HEAD)"
