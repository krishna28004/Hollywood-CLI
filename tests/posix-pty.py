#!/usr/bin/env python3
"""Small PTY end-to-end check for native Linux/macOS builds; uses only Python stdlib."""

import errno
import fcntl
import os
import pty
import re
import select
import signal
import subprocess
import struct
import sys
import termios
import time

ANSI_SEQUENCE = re.compile(rb"\x1b(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1b\\))")


def visible_text(data):
    return ANSI_SEQUENCE.sub(b"", data)


class Session:
    def __init__(self, executable, columns=80, rows=24):
        self.pid, self.master = pty.fork()
        if self.pid == 0:
            os.environ.setdefault("TERM", "xterm-256color")
            os.execv(executable, [executable])
        self.output = bytearray()
        self.status = None
        self.set_size(columns, rows)

    def set_size(self, columns, rows):
        fcntl.ioctl(self.master, termios.TIOCSWINSZ, struct.pack("HHHH", rows, columns, 0, 0))

    def read_for(self, duration):
        deadline = time.monotonic() + duration
        while time.monotonic() < deadline:
            wait = max(0, min(0.05, deadline - time.monotonic()))
            ready, _, _ = select.select([self.master], [], [], wait)
            if not ready:
                continue
            try:
                chunk = os.read(self.master, 65536)
            except OSError as exc:
                if exc.errno == errno.EIO:
                    break
                raise
            if not chunk:
                break
            self.output.extend(chunk)
            if len(self.output) > 1_000_000:
                del self.output[:-500_000]

    def wait_for(self, token, timeout=5, offset=0):
        encoded = token.encode()
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if encoded in visible_text(self.output)[offset:]:
                return
            if self.exit_code() is not None:
                raise AssertionError(f"process exited before {token!r}: {self.exit_code()}")
            self.read_for(0.05)
        raise AssertionError(f"timed out waiting for visible text {token!r}")

    def send(self, data):
        os.write(self.master, data)

    def exit_code(self):
        if self.status is not None:
            return self.status
        waited, status = os.waitpid(self.pid, os.WNOHANG)
        if waited:
            self.status = os.waitstatus_to_exitcode(status)
        return self.status

    def wait_exit(self, timeout=5):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            self.read_for(0.05)
            code = self.exit_code()
            if code is not None:
                return code
        raise AssertionError("process did not exit before timeout")

    def close(self):
        code = self.exit_code()
        if code is None:
            try:
                os.kill(self.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            os.waitpid(self.pid, 0)
        os.close(self.master)


def main():
    if len(sys.argv) != 2:
        print("Usage: posix-pty.py /absolute/path/to/hollywood", file=sys.stderr)
        return 2
    executable = os.path.abspath(sys.argv[1])
    if not os.path.isfile(executable):
        raise FileNotFoundError(executable)

    redirected = subprocess.run([executable, "--no-config"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=5)
    if redirected.returncode != 3 or redirected.stdout or b"\x1b" in redirected.stderr:
        raise AssertionError("redirected interactive launch must fail cleanly without emitting VT output")
    snapshot = subprocess.run([executable, "--no-config", "--snapshot", "--seed", "42"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=5)
    if snapshot.returncode != 0 or not snapshot.stdout or b"\x1b" in snapshot.stdout:
        raise AssertionError("redirected snapshot must produce plain text and exit cleanly")

    session = Session(executable)
    try:
        session.wait_for("HOLLYWOOD")
        terminal_attributes = termios.tcgetattr(session.master)
        if terminal_attributes[3] & (termios.ICANON | termios.ECHO):
            raise AssertionError("interactive adapter did not enable noncanonical, no-echo input")
        session.read_for(0.3)
        if b"HOLLYWOOD" not in session.output:
            raise AssertionError("scene header was not captured")

        session.send(b" ")
        session.wait_for("SPACE RESUME", offset=len(visible_text(session.output)))
        session.read_for(0.3)
        paused_bytes = len(session.output)
        session.read_for(0.3)
        if len(session.output) != paused_bytes:
            raise AssertionError("terminal output changed while paused")

        session.send(b" ")
        session.wait_for("SPACE PAUSE", offset=len(visible_text(session.output)), timeout=3)

        session.set_size(12, 5)
        session.wait_for("RESIZE")
        session.set_size(120, 30)
        session.read_for(0.3)
        session.send(b"q")
        if session.wait_exit() != 0:
            raise AssertionError("Q did not exit with status 0")
        if b"\x1b[?25h" not in session.output:
            raise AssertionError("cursor-show restoration was not emitted")
    finally:
        session.close()

    escape = Session(executable)
    try:
        escape.wait_for("HOLLYWOOD")
        escape.send(b"\x1b")
        if escape.wait_exit() != 0:
            raise AssertionError("Escape did not exit with status 0")
    finally:
        escape.close()

    interrupt = Session(executable)
    try:
        interrupt.wait_for("HOLLYWOOD")
        interrupt.send(b"\x03")
        if interrupt.wait_exit() != 130:
            raise AssertionError("Ctrl+C did not exit with status 130")
        if b"\x1b[?25h" not in interrupt.output:
            raise AssertionError("Ctrl+C did not restore cursor visibility")
    finally:
        interrupt.close()

    terminate = Session(executable)
    try:
        terminate.wait_for("HOLLYWOOD")
        os.kill(terminate.pid, signal.SIGTERM)
        if terminate.wait_exit() != 143:
            raise AssertionError("SIGTERM did not exit with status 143")
        if b"\x1b[?25h" not in terminate.output:
            raise AssertionError("SIGTERM did not restore cursor visibility")
    finally:
        terminate.close()

    print("PASS: POSIX PTY scene, animation, pause/resume, resize, Q/Escape/Ctrl+C/SIGTERM, cursor restoration")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exception:
        print(f"FAIL: {exception}", file=sys.stderr)
        sys.exit(1)
