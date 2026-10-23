"""Linux/root smoke checks using disposable files and stubbed services/health calls."""

import os
import pathlib
import stat
import subprocess
import sys
import tempfile


def check_release(source: str, healthy: bool) -> None:
    with tempfile.TemporaryDirectory(prefix="aljawhara-release-check-") as directory:
        root = pathlib.Path(directory)
        base = root / "app"
        live = base / "publish"
        live.mkdir(parents=True)
        (live / "Ecom.API.dll").write_text("previous")
        configuration = '{"test":"server-only"}'
        (live / "appsettings.json").write_text(configuration)

        revision = "a" * 40
        staged = base / "incoming" / revision
        (staged / "deploy").mkdir(parents=True)
        (staged / "Ecom.API.dll").write_text("new")
        (staged / "Ecom.API.runtimeconfig.json").write_text("{}")
        (staged / "deploy/install-invoice-browser.sh").write_text("#!/bin/bash\nexit 0\n")

        executables = root / "bin"
        executables.mkdir()
        commands = {"systemctl": "exit 0", "curl": "exit " + ("0" if healthy else "1"), "sleep": "exit 0"}
        for name, command in commands.items():
            executable = executables / name
            executable.write_text("#!/bin/bash\n" + command + "\n")
            executable.chmod(0o755)

        # Redirect every real filesystem target into the disposable test directory.
        isolated = source
        replacements = {
            "base=/var/www/Aljawhara.NET": "base=" + str(base),
            "/run/lock/aljawhara-deploy.lock": str(root / "deploy.lock"),
            "/var/lib/aljawhara/monitoring": str(root / "monitoring"),
        }
        for original, replacement in replacements.items():
            assert original in isolated
            isolated = isolated.replace(original, replacement)
        script = root / "activate.sh"
        script.write_text(isolated)
        result = subprocess.run(
            ["bash", str(script), revision],
            env={**os.environ, "PATH": str(executables) + ":" + os.environ["PATH"]},
            capture_output=True,
            text=True,
        )
        assert (result.returncode == 0) == healthy, (result.stdout, result.stderr)
        assert (live / "Ecom.API.dll").read_text() == ("new" if healthy else "previous")
        assert (live / "appsettings.json").read_text() == configuration
        suffix = "previous" if healthy else "failed"
        retained = list((base / "releases").glob("*-" + suffix + "/Ecom.API.dll"))
        assert len(retained) == 1
        assert retained[0].read_text() == ("previous" if healthy else "new")
        if healthy:
            assert stat.S_IMODE((live / "appsettings.json").stat().st_mode) == 0o640
        print("DEPLOYMENT_TEST_PASSED", "successful activation" if healthy else "automatic rollback")


if __name__ == "__main__":
    script_path = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else pathlib.Path(__file__).resolve().parents[1] / "deploy/ubuntu/activate-release.sh"
    subprocess.run(["bash", "-n", str(script_path)], check=True)
    for healthy in (True, False):
        check_release(script_path.read_text(), healthy)
