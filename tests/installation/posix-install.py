"""Exercise the packaged installer with disposable homes and fresh shells."""

import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


archive = Path(sys.argv[1]).resolve()
version = sys.argv[2]
pty_check = Path(__file__).resolve().parents[1] / "posix-pty.py"


with tempfile.TemporaryDirectory(prefix="Hollywood install Ω ") as temp:
    root = Path(temp)
    package = root / "package"
    package.mkdir()
    subprocess.run(["tar", "-xzf", archive, "-C", package], check=True)
    installer = package / "install.sh"
    tools = root / "tools"
    tools.mkdir()
    for name in ("cat", "chmod", "cp", "dirname", "id", "ln", "mkdir", "mktemp", "mv", "readlink", "rm", "rmdir"):
        (tools / name).symlink_to(shutil.which(name))
    home = root / "user home"
    home.mkdir()
    bin_dir = home / ".local/bin"
    config = home / ".config/hollywood/config.json"
    config.parent.mkdir(parents=True)
    config.write_text('{"sentinel":true}\n', encoding="utf-8")
    env = {key: value for key, value in os.environ.items() if not key.upper().startswith("DOTNET_")}
    env.update(HOME=str(home), XDG_CONFIG_HOME=str(home / ".config"),
               DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(root / "runtime"), PATH=f"{bin_dir}:{tools}")

    def run(*command, success=True, environment=env):
        result = subprocess.run(command, cwd=root, env=environment, text=True, capture_output=True, timeout=30)
        if (result.returncode == 0) != success:
            raise AssertionError(f"Unexpected exit {result.returncode} for {command}\n{result.stdout}{result.stderr}")
        return result.stdout

    def check(name):
        output = run("/bin/sh", "-c", f'command -v {name} && {name} --version')
        assert output.splitlines() == [str(bin_dir / name), f"Hollywood CLI {version}"], output

    run(installer, success=False, environment=dict(env, PATH=str(tools)))
    run(installer)
    check("hollywood")
    run(installer)
    check("hollywood")

    # An invalid replacement must leave the old executable and link in place.
    bad = root / "bad archive"
    bad.mkdir()
    shutil.copy2(installer, bad / "install.sh")
    fake = bad / "hollywood"
    fake.write_text("#!/bin/sh\nexit 1\n", encoding="ascii")
    fake.chmod(0o755)
    run(bad / "install.sh", success=False)
    check("hollywood")

    # Ignore the synthetic user configuration for the full installed PTY check.
    subprocess.run([sys.executable, pty_check, bin_dir / "hollywood"], cwd=root,
                   env=dict(env, XDG_CONFIG_HOME=str(root / "empty")), check=True)
    run(home / ".local/share/hollywood-cli/uninstall.sh", "--uninstall")
    run("/bin/sh", "-c", "command -v hollywood", success=False)
    assert config.read_text(encoding="utf-8") == '{"sentinel":true}\n'

    unrelated = bin_dir / "hollywood"
    unrelated.write_text("#!/bin/sh\nprintf unrelated\\n\n", encoding="ascii")
    unrelated.chmod(0o755)
    original = unrelated.read_bytes()
    run(installer)
    check("hollywood-cli")
    run(installer, "--uninstall")
    assert unrelated.read_bytes() == original
    assert not (bin_dir / "hollywood-cli").is_symlink()
    assert config.read_text(encoding="utf-8") == '{"sentinel":true}\n'

print("PASS: packaged POSIX install, fresh shells, repair, failed replacement, PTY, collision, and uninstall")
