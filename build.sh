#!/usr/bin/env sh
set -eu

section=${1-all}
pwsh -NoLogo -NoProfile -File ./packaging/Invoke-Build.ps1 \
    -Section "$section" \
    -Configuration Debug
