#!/usr/bin/env bash
# Stage GitHub Release assets from Build ILSpy action artifacts.
# Usage: bash BuildTools/stage-release-assets.sh <artifacts-dir> <output-dir>
set -euo pipefail

artifact_root="${1:-release-artifacts}"
output_dir="${2:-release-assets}"

mkdir -p "$output_dir"

find_one() {
  local file_name="$1"
  local dir_pattern="$2"
  local -a matches=()
  local file dir_name

  while IFS= read -r -d '' file; do
    dir_name="$(basename "$(dirname "$file")")"
    if [[ "$dir_name" == $dir_pattern ]]; then
      matches+=("$file")
    fi
  done < <(find "$artifact_root" -type f -name "$file_name" -print0)

  if [[ ${#matches[@]} -ne 1 ]]; then
    printf 'Expected exactly one file named "%s" in directory matching "%s", found %d\n' \
      "$file_name" "$dir_pattern" "${#matches[@]}" >&2
    printf '  %s\n' "${matches[@]}" >&2
    return 1
  fi

  printf '%s' "${matches[0]}"
}

copy_asset() {
  local source="$1"
  local destination="$2"
  install -m 0644 "$source" "$output_dir/$destination"
}

win_zip="$(find_one 'ILSpy_binaries.zip' 'ILSpy * (Release)')"
version="$(basename "$(dirname "$win_zip")" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+' | head -n 1)"

if [[ -z "$version" ]]; then
  printf 'Could not determine build version from %s\n' "$win_zip" >&2
  exit 1
fi

copy_asset "$win_zip" "ILSpy_windows_${version}-x64.zip"
copy_asset "$(find_one 'ILSpy_binaries_arm64.zip' 'ILSpy arm64 * (Release)')" \
  "ILSpy_windows_${version}-arm64.zip"
copy_asset "$(find_one 'ILSpy_selfcontained_x64.zip' 'ILSpy self-contained x64 * (Release)')" \
  "ILSpy_windows_selfcontained_${version}-x64.zip"
copy_asset "$(find_one "ILSpy-${version}-x64.msi" 'ILSpy Installer x64 * (Release)')" \
  "ILSpy_Installer_${version}-x64.msi"
copy_asset "$(find_one "ILSpy-${version}-arm64.msi" 'ILSpy Installer arm64 * (Release)')" \
  "ILSpy_Installer_${version}-arm64.msi"
copy_asset "$(find_one 'ILSpy.AddIn.VS2022.vsix' 'ILSpy VS Addin for VS 2022 * (Release)')" \
  "ILSpy.AddIn.VS2022_${version}.vsix"

# Platform bundles that already use the upstream release naming convention.
while IFS= read -r -d '' file; do
  copy_asset "$file" "$(basename "$file")"
done < <(find "$artifact_root" -type f \( \
  -name "ILSpy_linux-*_${version}.zip" \
  -o -name "ilspy_${version}-1_*.deb" \
  -o -name "ilspy-${version}-1.*.rpm" \
  -o -name "ILSpy_macos-arm64_${version}.zip" \
  -o -name "ILSpy_Linux_AppImage_${version}*" \
  -o -name "ILSpy_AppImage_${version}*" \
  \) -print0)

printf 'version=%s\n' "$version"
printf 'staged %d release assets in %s\n' "$(find "$output_dir" -maxdepth 1 -type f | wc -l)" "$output_dir"
