#!/usr/bin/env sh
set -eu

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repository_root=$(cd "$script_directory/../.." && pwd)
cd "$repository_root"

command -v dotnet >/dev/null 2>&1 || {
    printf '%s\n' 'The .NET SDK is required but was not found on PATH.' >&2
    exit 1
}
command -v kitty >/dev/null 2>&1 || {
    printf '%s\n' 'kitty is required but was not found on PATH.' >&2
    exit 1
}
if [ -z "${KITTY_WINDOW_ID:-}" ]; then
    printf '%s\n' 'This launcher must be run from an active Kitty terminal session.' >&2
    exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
    printf '%s\n' 'Live evidence requires a clean checkout so the source commit identifies the tested code.' >&2
    exit 1
fi
source_commit=$(git rev-parse HEAD)
terminal_version=$(kitty --version)
if [ -r /etc/os-release ]; then
    # shellcheck disable=SC1091
    . /etc/os-release
    operating_system=${NAME:-Linux}
    operating_system_version=${VERSION_ID:-unknown}
else
    operating_system=$(uname -s)
    operating_system_version=$(uname -r)
fi

wsl_version=${WSL_VERSION:-}
if [ -z "$wsl_version" ]; then
    printf '%s' 'Exact WSL version (for example 2.6.1.0): ' >&2
    IFS= read -r wsl_version
fi
if [ -z "$wsl_version" ]; then
    printf '%s\n' 'An exact WSL version is required to qualify this transport.' >&2
    exit 1
fi

run_directory=$(mktemp -d "${TMPDIR:-/tmp}/Icod.Terminal-Environment.XXXXXX")
matrix="$run_directory/matrix.md"
identity_evidence="$run_directory/kitty-appearance-query.json"
dimensions_evidence="$run_directory/kitty-appearance-reporting.json"
resize_evidence="$run_directory/kitty-in-band-resize.json"
project=samples/Icod.Terminal.Compatibility.Sample/Icod.Terminal.Compatibility.Sample.csproj

run_sample() {
    dotnet run --project "$project" -c Staging -f net10.0 -- "$@"
}

run_sample --help
run_sample --list-scenarios
run_sample --render-matrix docs/compatibility/evidence/1.27.0 "$matrix" --release-version 1.27.0

run_sample --run query.appearance --terminal kitty --terminal-version "$terminal_version" --os "$operating_system" --os-version "$operating_system_version" --source-commit "$source_commit" --transport WSL --transport-version "$wsl_version" --output "$identity_evidence"
run_sample --run environment.appearance-reporting --terminal kitty --terminal-version "$terminal_version" --os "$operating_system" --os-version "$operating_system_version" --source-commit "$source_commit" --transport WSL --transport-version "$wsl_version" --output "$dimensions_evidence"
run_sample --run environment.in-band-resize --terminal kitty --terminal-version "$terminal_version" --os "$operating_system" --os-version "$operating_system_version" --source-commit "$source_commit" --transport WSL --transport-version "$wsl_version" --output "$resize_evidence"

printf '%s\n' ''
printf '%s\n' 'Safe Kitty/WSL compatibility evidence was written to:'
printf '  %s\n' "$identity_evidence"
printf '  %s\n' "$dimensions_evidence"
printf '  %s\n' "$resize_evidence"
printf '%s\n' 'Review the reports before accepting them into the versioned evidence directory.'
