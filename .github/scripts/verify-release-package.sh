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
  dotnet msbuild Icod.Terminal.csproj     -nologo     -getProperty:PackageVersion
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

dotnet run   --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj   -c "${configuration}"   --no-build
