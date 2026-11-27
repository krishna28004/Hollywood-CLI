"""Package and smoke-test the exact self-contained executable built by CI."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import tarfile
import tempfile
import zipfile


ROOT = Path(__file__).resolve().parents[2]


def runtime_pack(rid, assets):
    assets = json.loads(assets.read_text(encoding="utf-8-sig"))
    for framework in assets["project"]["frameworks"].values():
        for package in framework.get("downloadDependencies", []):
            name = f"Microsoft.NETCore.App.Runtime.{rid}"
            if package["name"].lower() != name.lower():
                continue
            version = package["version"].strip("[]").split(",")[0]
            for folder in assets["packageFolders"]:
                candidate = Path(folder) / name.lower() / version
                if (candidate / "THIRD-PARTY-NOTICES.TXT").is_file():
                    return candidate, version
    raise ValueError(f"Cannot locate the restored .NET runtime pack for {rid}")


def architecture(executable, rid):
    os_name, cpu = rid.split("-")
    with executable.open("rb") as f:
        header = f.read(64)
        if os_name == "win" and header[:2] == b"MZ":
            f.seek(struct.unpack_from("<I", header, 60)[0])
            signature, machine = struct.unpack("<4sH", f.read(6))
            if signature == b"PE\0\0":
                return machine == {"x64": 0x8664, "arm64": 0xAA64}[cpu]
        if os_name == "linux" and header[:6] == b"\x7fELF\x02\x01":
            return struct.unpack_from("<H", header, 18)[0] == {"x64": 62, "arm64": 183}[cpu]
        if os_name == "osx" and header[:4] == b"\xcf\xfa\xed\xfe":
            return struct.unpack_from("<I", header, 4)[0] == {"x64": 0x01000007, "arm64": 0x0100000C}[cpu]
    return False


def sha(path):
    with path.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def verify(archive, stage, executable_name, rid, version):
    with tempfile.TemporaryDirectory(prefix="hollywood-verify-") as directory:
        directory = Path(directory)
        if archive.suffix == ".zip":
            with zipfile.ZipFile(archive) as package:
                if sorted(package.namelist()) != sorted(file.name for file in stage.iterdir()):
                    raise ValueError("Unexpected archive members")
                package.extractall(directory)
        else:
            with tarfile.open(archive) as package:
                members = package.getmembers()
                if sorted(member.name for member in members) != sorted(file.name for file in stage.iterdir()):
                    raise ValueError("Unexpected archive members")
                if not all(member.isfile() for member in members):
                    raise ValueError("Archive contains a non-file member")
                package.extractall(directory, filter="data")

        for file in stage.iterdir():
            if sha(file) != sha(directory / file.name):
                raise ValueError(f"Archive changed {file.name}")
        executable = directory / executable_name
        if not architecture(executable, rid) or not os.access(executable, os.X_OK):
            raise ValueError("Packaged application has an incorrect architecture or file mode")
        # The app must run directly with neither dotnet nor Git on PATH.
        env = {key: value for key, value in os.environ.items() if not key.upper().startswith("DOTNET_")}
        env.update(PATH="", DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(directory / "runtime"))
        output = subprocess.check_output([str(executable), "--version"], cwd=directory, env=env, timeout=30, text=True)
        if output.strip() != f"Hollywood CLI {version}":
            raise ValueError(f"Unexpected version from packaged application: {output!r}")
        snapshot = subprocess.check_output(
            [str(executable), "--snapshot", "--no-config", "--seed", "42"], cwd=directory, env=env, timeout=30
        )
        if b"HOLLYWOOD SIM" not in snapshot or b"\x1b" in snapshot:
            raise ValueError("Packaged application could not render a plain snapshot")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", required=True, choices=[
        "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64",
    ])
    parser.add_argument("--version", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--publish", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-beta\.[1-9][0-9]*)?", args.version):
        parser.error("expected a version such as 0.2.0 or 0.2.0-beta.1")
    if not re.fullmatch(r"[0-9a-f]{40}", args.commit):
        parser.error("expected the full source commit SHA")
    is_windows = args.rid.startswith("win-")
    name = "hollywood.exe" if is_windows else "hollywood"
    pack, runtime_version = runtime_pack(args.rid, ROOT / "src/Hollywood.Cli/obj/project.assets.json")
    runtime_name = f"Microsoft.NETCore.App.Runtime.{args.rid}"
    archive = args.output / f"hollywood-{args.rid}{'.zip' if is_windows else '.tar.gz'}"
    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="hollywood-package-") as directory:
        stage = Path(directory)
        shutil.copy2(args.publish / name, stage / name)
        shutil.copyfile(pack / "LICENSE.TXT", stage / "DOTNET-LICENSE.txt")
        shutil.copyfile(pack / "THIRD-PARTY-NOTICES.TXT", stage / "THIRD-PARTY-NOTICES.txt")
        shutil.copyfile(ROOT / "docs/installation.md", stage / "INSTALL.md")
        for optional in ("LICENSE", "NOTICE"):
            if (ROOT / optional).is_file():
                shutil.copyfile(ROOT / optional, stage / optional)
        if not is_windows:
            shutil.copyfile(ROOT / "packaging/posix/install.sh", stage / "install.sh")
            (stage / "install.sh").chmod(0o755)
            (stage / name).chmod(0o755)
        app = {"type": "application", "name": "Hollywood CLI", "version": args.version, "bom-ref": "hollywood"}
        runtime = {
            "type": "framework", "name": runtime_name, "version": runtime_version,
            "bom-ref": "dotnet-runtime", "purl": f"pkg:nuget/{runtime_name}@{runtime_version}",
        }
        sbom = {
            "bomFormat": "CycloneDX", "specVersion": "1.6", "version": 1, "metadata": {"component": app},
            "components": [runtime], "dependencies": [{"ref": "hollywood", "dependsOn": ["dotnet-runtime"]}],
        }
        (stage / "sbom.cdx.json").write_text(json.dumps(sbom, indent=2) + "\n", encoding="utf-8")
        manifest = {
            "version": args.version, "commit": args.commit, "runtimeIdentifier": args.rid,
            "dotnetRuntimeVersion": runtime_version, "appLicenseIncluded": (stage / "LICENSE").exists(),
            "sha256": {file.name: sha(file) for file in sorted(stage.iterdir())},
        }
        (stage / "artifact-metadata.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        if is_windows:
            with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
                for file in sorted(stage.iterdir()):
                    package.write(file, file.name)
        else:
            with tarfile.open(archive, "w:gz") as package:
                for file in sorted(stage.iterdir()):
                    package.add(file, arcname=file.name, recursive=False)
        verify(archive, stage, name, args.rid, args.version)
    digest = sha(archive)
    archive.with_name(archive.name + ".sha256").write_bytes(f"{digest}  {archive.name}\n".encode("ascii"))
    print(f"PASS {args.rid} {args.version} {digest}  {archive.name}")


if __name__ == "__main__":
    main()
