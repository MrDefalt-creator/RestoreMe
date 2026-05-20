#!/usr/bin/env bash
#
# RestoreMe Agent installer for Linux.
#
# Usage:
#   sudo ./install-agent.sh --server https://restoreme.example.com --token <enrollment-token>
#
# Or remote one-liner:
#   curl -fsSL https://github.com/MrDefalt-creator/RestorMe/releases/latest/download/install-agent.sh \
#     | sudo bash -s -- --server https://restoreme.example.com --token <enrollment-token>
#
# Uninstall:
#   sudo ./install-agent.sh --uninstall
#
set -euo pipefail

REPO="MrDefalt-creator/RestorMe"
VERSION="latest"
BIN_DIR="/opt/restoreme-agent"
CONFIG_DIR="/etc/restoreme-agent"
STATE_DIR="/var/lib/restoreme-agent/state"
SERVICE_NAME="restoreme-agent"
SERVICE_USER="root"
SERVER=""
TOKEN=""
MODE="install"

usage() {
  cat <<EOF
RestoreMe Agent installer

Required (install):
  --server URL              Backend base URL, e.g. https://restoreme.example.com
  --token  TOKEN            Enrollment token from the admin panel

Optional:
  --version vX.Y.Z          Release tag to install (default: latest)
  --state-dir PATH          Override agent state directory (default: $STATE_DIR)
  --service-user USER       systemd service User= (default: $SERVICE_USER)
                            Use 'root' for filesystem backups of arbitrary paths.
  --repo OWNER/NAME         GitHub repository to pull releases from (default: $REPO)
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
    --version) VERSION="$2"; shift 2 ;;
    --state-dir) STATE_DIR="$2"; shift 2 ;;
    --service-user) SERVICE_USER="$2"; shift 2 ;;
    --repo) REPO="$2"; shift 2 ;;
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

ASSET="restoreme-agent-${RID}"
if [[ "$VERSION" == "latest" ]]; then
  DOWNLOAD_URL="https://github.com/${REPO}/releases/latest/download/${ASSET}"
else
  DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${VERSION}/${ASSET}"
fi

echo "==> Downloading: $DOWNLOAD_URL"
mkdir -p "$BIN_DIR" "$CONFIG_DIR" "$STATE_DIR"
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT
curl -fSL --retry 3 --retry-delay 2 "$DOWNLOAD_URL" -o "$TMP"
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
Documentation=https://github.com/${REPO}
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
