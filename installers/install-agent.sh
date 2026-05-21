#!/usr/bin/env bash
#
# RestoreMe Agent installer for Linux.
#
# The binary is pulled from the backend that minted the enrollment token —
# no GitHub dependency, no public release pipeline required. The backend
# must serve agent binaries at /installers/binaries/restoreme-agent-<RID>.
# See docker-compose/README.md -> "Building agent binaries".
#
# Usage:
#   sudo ./install-agent.sh --server http://restoreme.lan:8080 --token <enrollment-token>
#
# Or remote one-liner via the install wizard:
#   curl -fsSL http://restoreme.lan:8080/installers/install-agent.sh \
#     | sudo bash -s -- --server http://restoreme.lan:8080 --token <enrollment-token>
#
# Uninstall:
#   sudo ./install-agent.sh --uninstall
#
set -euo pipefail

BIN_DIR="/opt/restoreme-agent"
CONFIG_DIR="/etc/restoreme-agent"
STATE_DIR="/var/lib/restoreme-agent/state"
SERVICE_NAME="restoreme-agent"
SERVICE_USER="root"
SERVER=""
TOKEN=""
BINARY_URL=""
MODE="install"

usage() {
  cat <<EOF
RestoreMe Agent installer

Required (install):
  --server URL              Backend base URL, e.g. http://restoreme.lan:8080
  --token  TOKEN            Enrollment token from the admin panel

Optional:
  --state-dir PATH          Override agent state directory (default: $STATE_DIR)
  --service-user USER       systemd service User= (default: $SERVICE_USER)
                            Use 'root' for filesystem backups of arbitrary paths.
  --binary-url URL          Override the agent binary download URL.
                            Default: \$SERVER/installers/binaries/restoreme-agent-<RID>
  --uninstall               Stop the service and remove binary/config/unit.
                            State directory is preserved unless --purge is given.
  --purge                   With --uninstall, also delete $STATE_DIR.
  -h, --help                Show this message.
EOF
}

PURGE=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --server) SERVER="$2"; shift 2 ;;
    --token) TOKEN="$2"; shift 2 ;;
    --state-dir) STATE_DIR="$2"; shift 2 ;;
    --service-user) SERVICE_USER="$2"; shift 2 ;;
    --binary-url) BINARY_URL="$2"; shift 2 ;;
    --uninstall) MODE="uninstall"; shift ;;
    --purge) PURGE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown flag: $1" >&2; usage; exit 1 ;;
  esac
done

if [[ $EUID -ne 0 ]]; then
  echo "This installer must be run as root (use sudo)." >&2
  exit 1
fi

if ! command -v systemctl >/dev/null 2>&1; then
  echo "systemctl not found. This installer targets systemd-based distributions." >&2
  exit 1
fi

if [[ "$MODE" == "uninstall" ]]; then
  echo "==> Stopping $SERVICE_NAME"
  systemctl disable --now "$SERVICE_NAME" 2>/dev/null || true
  rm -f "/etc/systemd/system/${SERVICE_NAME}.service"
  systemctl daemon-reload

  rm -rf "$BIN_DIR" "$CONFIG_DIR"

  if [[ $PURGE -eq 1 ]]; then
    rm -rf "$STATE_DIR"
    echo "==> Purged $STATE_DIR"
  else
    echo "==> State preserved at $STATE_DIR. Re-run with --purge to delete it."
  fi
  echo "Uninstall complete."
  exit 0
fi

if [[ -z "$SERVER" || -z "$TOKEN" ]]; then
  echo "Both --server and --token are required for install." >&2
  usage
  exit 1
fi

ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64) RID="linux-x64" ;;
  aarch64|arm64) RID="linux-arm64" ;;
  *) echo "Unsupported architecture: $ARCH (supported: x86_64, aarch64)" >&2; exit 1 ;;
esac

if [[ -z "$BINARY_URL" ]]; then
  # Strip trailing slash on $SERVER before joining so we don't ship //installers/...
  BINARY_URL="${SERVER%/}/installers/binaries/restoreme-agent-${RID}"
fi

echo "==> Downloading: $BINARY_URL"
mkdir -p "$BIN_DIR" "$CONFIG_DIR" "$STATE_DIR"
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT
# Capture HTTP status separately so we can give a friendlier hint on 404.
http_status=$(curl -fSL --retry 3 --retry-delay 2 -w '%{http_code}' -o "$TMP" "$BINARY_URL" || echo "000")
if [[ ! -s "$TMP" || "$http_status" != "200" ]]; then
  echo >&2
  echo "Agent binary not found at $BINARY_URL (HTTP $http_status)" >&2
  echo "The backend does not have published agent binaries yet." >&2
  echo "On the host running the backend, publish them once with:" >&2
  echo "  docker compose --profile build-agents up agent-builder" >&2
  echo 'See docker-compose/README.md -> "Building agent binaries" for details.' >&2
  exit 1
fi
install -m 0755 "$TMP" "$BIN_DIR/restoreme-agent"

if [[ "$SERVICE_USER" != "root" ]]; then
  if ! id -u "$SERVICE_USER" >/dev/null 2>&1; then
    echo "==> Creating system user: $SERVICE_USER"
    useradd --system --home-dir "$STATE_DIR" --shell /usr/sbin/nologin "$SERVICE_USER"
  fi
  chown -R "$SERVICE_USER:$SERVICE_USER" "$STATE_DIR"
fi

echo "==> Writing config: $CONFIG_DIR/config.env"
umask 077
cat > "$CONFIG_DIR/config.env" <<EOF
RESTOREME_SERVER=$SERVER
RESTOREME_ENROLLMENT_TOKEN=$TOKEN
RESTOREME_STATE_DIR=$STATE_DIR
EOF
chmod 600 "$CONFIG_DIR/config.env"
umask 022

echo "==> Writing systemd unit: /etc/systemd/system/${SERVICE_NAME}.service"
cat > "/etc/systemd/system/${SERVICE_NAME}.service" <<EOF
[Unit]
Description=RestoreMe Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=${SERVICE_USER}
EnvironmentFile=${CONFIG_DIR}/config.env
ExecStart=${BIN_DIR}/restoreme-agent
Restart=always
RestartSec=10
WorkingDirectory=${STATE_DIR}

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable --now "$SERVICE_NAME"

echo
echo "==> Status"
systemctl status "$SERVICE_NAME" --no-pager --lines=5 || true
echo
echo "Follow logs with: journalctl -u $SERVICE_NAME -f"
