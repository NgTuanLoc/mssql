#!/usr/bin/env bash
# Opens an interactive sqlcmd session in the mssql-learn container.
set -euo pipefail

ENV_FILE="$(cd "$(dirname "$0")/../.." && pwd)/docker/.env"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: .env not found at $ENV_FILE" >&2
  exit 1
fi

SA_PASSWORD=$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)
if [[ -z "$SA_PASSWORD" ]]; then
  echo "ERROR: SA_PASSWORD not found in $ENV_FILE" >&2
  exit 1
fi

CONTAINER=$(command -v podman &>/dev/null && echo podman || echo docker)
echo "Connecting to mssql-learn as sa..."
"$CONTAINER" exec -it mssql-learn /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -No
