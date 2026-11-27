"""Scan tracked Git content without printing matching values."""

import re
import subprocess
import sys


SIGNATURES = {
    "private key header": rb"-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED |PGP )?PRIVATE KEY(?: BLOCK)?-----",
    "GitHub token": rb"\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})\b",
    "AWS key identifier": rb"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b",
    "Slack token": rb"\bxox[baprs]-[0-9A-Za-z-]{15,}\b",
    "Google API key": rb"\bAIza[0-9A-Za-z_-]{35}\b",
    "service API key": rb"\bsk-(?:proj-|live_)?[A-Za-z0-9_-]{28,}\b",
    "embedded URL credentials": rb"https?://[a-zA-Z0-9_.%+-]+:[a-zA-Z0-9_.%+-]{6,}@",
    "literal secret assignment": rb"""(?i)\b(?:api[_-]?key|client[_-]?secret|password|passwd|access[_-]?token)\b\s*[:=]\s*["'][A-Za-z0-9_.+/!@%$-]{12,}["']""",
}
FORBIDDEN = re.compile(
    rb"(?i)(?:^|/)(?:read materials|\.aws|\.codex|\.agents|\.env(?:\.[^/]*)?|id_rsa|id_ed25519)(?:/|$)"
    rb"|\.(?:pfx|p12|key|pem|exe|dll|msi|zip|tar|gz|deb|rpm|pkg|dmg)$"
)


def git(*args):
    return subprocess.check_output(["git", *args])


def main():
    findings = 0
    blobs = {}
    # Inspect each historical tree for private or generated paths, including renamed blobs.
    for commit in git("rev-list", "--all", "HEAD").splitlines():
        for record in git("ls-tree", "-rz", commit.decode()).split(b"\0"):
            if not record:
                continue
            metadata, path = record.split(b"\t", 1)
            _, kind, oid = metadata.split()
            if kind != b"blob":
                continue
            if FORBIDDEN.search(path):
                print(f"Restricted historical path in commit {commit[:12].decode()}; inspect locally.")
                findings += 1
            blobs.setdefault(oid, path)

    with subprocess.Popen(["git", "cat-file", "--batch"], stdin=subprocess.PIPE, stdout=subprocess.PIPE) as proc:
        for oid, path in blobs.items():
            proc.stdin.write(oid + b"\n")
            proc.stdin.flush()
            _, kind, size = proc.stdout.readline().split()
            payload = proc.stdout.read(int(size))
            proc.stdout.read(1)
            if payload.startswith((b"MZ", b"\x7fELF", b"\xcf\xfa\xed\xfe")):
                print(f"Unexpected executable blob {oid[:12].decode()}; inspect locally.")
                findings += 1
            for name, signature in SIGNATURES.items():
                if re.search(signature, payload):
                    print(f"Possible {name} in historical blob {oid[:12].decode()}; inspect locally.")
                    findings += 1
        proc.stdin.close()
        if proc.wait():
            raise RuntimeError("Could not read Git objects")
    print(f"Inspected {len(blobs)} tracked historical blobs. Findings: {findings}.")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
