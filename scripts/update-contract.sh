#!/usr/bin/env bash
# Regenera contracts/openapi.v1.json desde el código (requiere Docker para Testcontainers).
set -euo pipefail
cd "$(dirname "$0")/.."
UPDATE_CONTRACT=1 dotnet test tests/Encorely.Tests --filter "FullyQualifiedName~OpenApiContractTests" "$@"
echo "Contrato actualizado: contracts/openapi.v1.json (anota el cambio en contracts/CHANGELOG.md)"
