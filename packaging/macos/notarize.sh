#!/bin/bash
# Build a stapled DMG around the approved macOS candidate on a native Mac.
set -euo pipefail

if [[ $# != 2 || $(uname -s) != Darwin ]]; then
    echo "Usage on macOS: $0 VERIFIED_CANDIDATE_TARBALL ABSOLUTE_OUTPUT_DMG" >&2
    exit 2
fi
: "${HOLLYWOOD_DEVELOPER_ID:?Set the approved Developer ID Application identity}"
: "${HOLLYWOOD_NOTARY_PROFILE:?Set the local notarytool keychain profile name}"
[[ "$2" == /*.dmg && ! -e "$2" && ! -L "$2" ]] || { echo 'Use a new absolute .dmg path.' >&2; exit 2; }

scratch=$(mktemp -d)
cleanup() {
    for name in hollywood install.sh INSTALL.md LICENSE NOTICE DOTNET-LICENSE.txt THIRD-PARTY-NOTICES.txt artifact-metadata.json sbom.cdx.json; do
        rm -f "$stage/$name"
    done
    rmdir "$stage" 2>/dev/null || true
    rm -f "$scratch/entitlements.plist"
    rmdir "$scratch" 2>/dev/null || true
}
stage="$scratch/package"
trap cleanup EXIT
mkdir "$stage"
python3.12 - "$1" "$stage" <<'PY'
import hashlib
import json
from pathlib import Path
import sys
import tarfile

allowed = {"hollywood", "install.sh", "INSTALL.md", "LICENSE", "NOTICE", "DOTNET-LICENSE.txt",
           "THIRD-PARTY-NOTICES.txt", "artifact-metadata.json", "sbom.cdx.json"}
with tarfile.open(sys.argv[1]) as archive:
    members = archive.getmembers()
    names = {member.name for member in members}
    if names - allowed or len(names) != len(members) or not all(member.isfile() for member in members):
        raise SystemExit("Unexpected archive members")
    if not {"hollywood", "install.sh", "LICENSE", "artifact-metadata.json"} <= names:
        raise SystemExit("The approved license or package metadata is missing")
    archive.extractall(sys.argv[2], filter="data")

stage = Path(sys.argv[2])
metadata = json.loads((stage / "artifact-metadata.json").read_text())
if metadata["sha256"].keys() != names - {"artifact-metadata.json"}:
    raise SystemExit("The metadata does not cover all archive members")
for name, expected in metadata["sha256"].items():
    if hashlib.sha256((stage / name).read_bytes()).hexdigest() != expected:
        raise SystemExit(f"The archive checksum for {name} does not match")
PY
cat > "$scratch/entitlements.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0">
<dict><key>com.apple.security.cs.allow-jit</key><true/></dict>
</plist>
EOF

codesign --force --timestamp --options runtime --entitlements "$scratch/entitlements.plist" \
    --sign "$HOLLYWOOD_DEVELOPER_ID" "$stage/hollywood"
codesign --verify --strict "$stage/hollywood"
"$stage/hollywood" --version
python3.12 - "$stage" <<'PY'
import hashlib
import json
from pathlib import Path
import sys

stage = Path(sys.argv[1])
manifest = stage / "artifact-metadata.json"
metadata = json.loads(manifest.read_text())
metadata["sha256"]["hollywood"] = hashlib.sha256((stage / "hollywood").read_bytes()).hexdigest()
manifest.write_text(json.dumps(metadata, indent=2) + "\n")
PY
hdiutil create -quiet -volname 'Hollywood CLI' -srcfolder "$stage" -format UDZO "$2"
codesign --timestamp --sign "$HOLLYWOOD_DEVELOPER_ID" "$2"
xcrun notarytool submit "$2" --keychain-profile "$HOLLYWOOD_NOTARY_PROFILE" --wait
xcrun stapler staple "$2"
xcrun stapler validate "$2"
spctl --assess --type open --context context:primary-signature "$2"
shasum -a 256 "$2" > "$2.sha256"
