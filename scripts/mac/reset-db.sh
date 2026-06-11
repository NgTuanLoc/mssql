#!/usr/bin/env bash
# Drops AdventureWorks2022 (and all lesson schemas inside it) then re-restores
# from the backup. Use this to recover from any data corruption or mistakes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/../../docker/.env"

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
echo "Dropping AdventureWorks2022..."

echo "IF DB_ID('AdventureWorks2022') IS NOT NULL \
BEGIN \
  ALTER DATABASE [AdventureWorks2022] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; \
  DROP DATABASE [AdventureWorks2022]; \
  PRINT 'Database dropped.'; \
END \
ELSE PRINT 'Database did not exist, skipping drop.';" \
| "$CONTAINER" exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -No

echo "Re-restoring AdventureWorks2022..."
bash "$SCRIPT_DIR/restore-adventureworks.sh"
