#!/usr/bin/env bash
# Restores AdventureWorks2022.bak into the mssql-learn container.
# Place AdventureWorks2022.bak in the backups/ directory first.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
ENV_FILE="$REPO_ROOT/docker/.env"
BAK_PATH="$REPO_ROOT/backups/AdventureWorks2022.bak"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: .env not found at $ENV_FILE" >&2
  exit 1
fi

SA_PASSWORD=$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)
if [[ -z "$SA_PASSWORD" ]]; then
  echo "ERROR: SA_PASSWORD not found in $ENV_FILE" >&2
  exit 1
fi

if [[ ! -f "$BAK_PATH" ]]; then
  echo "ERROR: AdventureWorks2022.bak not found at $BAK_PATH" >&2
  echo "See data/adventureworks/README.md for download instructions." >&2
  exit 1
fi

CONTAINER=$(command -v podman &>/dev/null && echo podman || echo docker)
echo "Restoring AdventureWorks2022 -- this takes ~30 seconds..."

echo "RESTORE DATABASE [AdventureWorks2022] \
FROM DISK = N'/var/opt/mssql/backups/AdventureWorks2022.bak' \
WITH MOVE N'AdventureWorks2022' TO N'/var/opt/mssql/data/AdventureWorks2022.mdf', \
     MOVE N'AdventureWorks2022_log' TO N'/var/opt/mssql/data/AdventureWorks2022_log.ldf', \
     REPLACE, STATS = 10;" \
| "$CONTAINER" exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -No

echo "Restore complete."
