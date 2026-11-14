#!/usr/bin/env bash
set -euo pipefail
# Compatibility entry for the Unity menu, not a synchronous lazy-load template.
exec bash "$(dirname -- "${BASH_SOURCE[0]}")/gen_code_bin_to_project.sh" "$@"
