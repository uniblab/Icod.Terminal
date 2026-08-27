#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: verify-release-package.sh <artifact-directory> <Staging|Release>" >&2
}

if (( $# != 2 )); then
  usage
  exit 2
fi

artifact_dir="$1"
configuration="$2"

case "${configuration}" in
  Staging|Release)
    ;;
  *)
    usage
    exit 2
    ;;
esac

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${repository_root}"

if [[ ! -d "${artifact_dir}" ]]; then
  echo "Artifact directory does not exist: ${artifact_dir}" >&2
  exit 1
fi

artifact_dir="$(cd "${artifact_dir}" && pwd)"

package_version="$(
  dotnet msbuild Icod.Terminal.csproj \
    -nologo \
    -getProperty:PackageVersion
)"
package_version="${package_version//$'\r'/}"

if [[ -z "${package_version}" ]]; then
  echo "Unable to determine PackageVersion." >&2
  exit 1
fi

package_path="${artifact_dir}/Icod.Terminal.${package_version}.nupkg"
symbols_path="${artifact_dir}/Icod.Terminal.${package_version}.snupkg"

if [[ ! -f "${package_path}" ]]; then
  echo "Missing package: ${package_path}" >&2
  exit 1
fi

if [[ ! -f "${symbols_path}" ]]; then
  echo "Missing symbols package: ${symbols_path}" >&2
  exit 1
fi

echo
echo "=== Verify package structure, dependency closure, symbols, and Source Link (${configuration}) ==="
dotnet run \
  --project tools/package-verifier/Icod.Terminal.PackageVerifier.csproj \
  -c "${configuration}" \
  -f net10.0 \
  -- "${artifact_dir}"

smoke_root="$(mktemp -d)"
trap 'rm -rf "${smoke_root}"' EXIT

cp \
  tools/package-smoke/Icod.Terminal.PackageSmoke.csproj \
  "${smoke_root}/Icod.Terminal.PackageSmoke.csproj"
cp \
  tools/package-smoke/Program.cs \
  "${smoke_root}/Program.cs"

(
  export NUGET_PACKAGES="${smoke_root}/packages"

  echo
  echo "=== Fresh package consumer restore ==="
  dotnet restore \
    "${smoke_root}/Icod.Terminal.PackageSmoke.csproj" \
    --no-cache \
    --source "${artifact_dir}" \
    --source "https://api.nuget.org/v3/index.json" \
    -p:IcodTerminalPackageVersion="${package_version}"

  echo
  echo "=== Fresh package consumer: net8.0 ==="
  dotnet run \
    --project "${smoke_root}/Icod.Terminal.PackageSmoke.csproj" \
    -c "${configuration}" \
    -f net8.0 \
    --no-restore \
    -p:IcodTerminalPackageVersion="${package_version}"

  echo
  echo "=== Fresh package consumer: net10.0 ==="
  dotnet run \
    --project "${smoke_root}/Icod.Terminal.PackageSmoke.csproj" \
    -c "${configuration}" \
    -f net10.0 \
    --no-restore \
    -p:IcodTerminalPackageVersion="${package_version}"
)
