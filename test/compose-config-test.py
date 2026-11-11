"""Check the ordinary Docker Compose entry point without starting containers."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent


class DefaultLayoutTests(unittest.TestCase):
    def resolve(self, environment):
        with tempfile.TemporaryDirectory(prefix="mineos-compose-") as directory:
            path = Path(directory)
            shutil.copy(ROOT / "docker-compose.yml", path)
            (path / ".env").write_text(environment, encoding="utf-8")
            # Do not let the developer's shell override the fixture.
            clean_env = {k: v for k, v in os.environ.items() if not k.startswith(
                ("COMPOSE_", "Auth__", "ApiKey__", "WEB_", "ORIGIN", "MINECRAFT_",
                 "Host__", "ConnectionStrings__", "HOST_BASE_", "Data__"))}
            result = subprocess.run(["docker", "compose", "config", "--format", "json"],
                                    cwd=path, env=clean_env, text=True, capture_output=True)
            return result

    def config(self, environment):
        result = self.resolve(environment)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("variable is not set", result.stderr)
        return json.loads(result.stdout)["services"]

    def test_standard_files_load_credentials_and_our_sources(self):
        config = self.config((ROOT / ".env.template").read_text(encoding="utf-8"))
        for service in config.values():
            self.assertIn("build", service)
            self.assertNotIn("freeman412", service["image"])
            self.assertEqual(service["pull_policy"], "never")
        api = config["api"]["environment"]
        self.assertEqual(api["Auth__SeedUsername"], "admin")
        self.assertEqual(api["Auth__SeedPassword"], "replace-with-a-unique-password")
        self.assertEqual(api["ConnectionStrings__Default"], "Data Source=/app/data/mineos.db")
        web = config["web"]
        self.assertEqual(str(web["ports"][0]["published"]), "6070")
        self.assertEqual(web["ports"][0]["host_ip"], "0.0.0.0")
        self.assertEqual(web["environment"]["ORIGIN"], "http://localhost:6070")
        self.assertEqual(str(web["environment"]["BODY_SIZE_LIMIT"]), "10737418240")
        self.assertEqual(str(web["environment"]["WEB_REQUEST_TIMEOUT_MS"]), "86400000")

    def test_previous_preview_settings_survive_renaming(self):
        config = self.config("Auth__SeedUsername=old-admin\nAuth__SeedPassword=existing-password\n"
                             "Auth__JwtSecret=existing-signing-key\nApiKey__SeedKey=existing-service-key\n"
                             "WEB_PORT=3000\nWEB_BIND_ADDRESS=127.0.0.1\n"
                             "WEB_ORIGIN=http://localhost:3000\nMINECRAFT_UID=1234\nMINECRAFT_GID=1235\n")
        self.assertEqual(config["api"]["environment"]["Auth__SeedPassword"], "existing-password")
        self.assertEqual(str(config["api"]["environment"]["Host__RunAsUid"]), "1234")
        self.assertEqual(str(config["api"]["environment"]["Host__RunAsGid"]), "1235")
        self.assertEqual(str(config["web"]["ports"][0]["published"]), "3000")
        self.assertEqual(config["web"]["ports"][0]["host_ip"], "127.0.0.1")
        self.assertEqual(config["web"]["environment"]["ORIGIN"], "http://localhost:3000")

    def test_missing_credentials_fail_before_startup(self):
        self.assertNotEqual(self.resolve("").returncode, 0)

    def test_storage_and_port_overrides_are_preserved(self):
        config = self.config("Auth__SeedPassword=password\nAuth__JwtSecret=key\nApiKey__SeedKey=key\n"
                             "HOST_BASE_DIRECTORY=./worlds\nData__Directory=./database\n"
                             "WEB_PORT=8080\nORIGIN=https://mineos.example.org\nHost__OwnerUid=2000\n")
        self.assertEqual(str(config["web"]["ports"][0]["published"]), "8080")
        self.assertEqual(config["web"]["environment"]["ORIGIN"], "https://mineos.example.org")
        self.assertEqual(str(config["api"]["environment"]["Host__RunAsUid"]), "2000")
        volumes = {v["target"]: Path(v["source"]).name for v in config["api"]["volumes"]}
        self.assertEqual(volumes["/var/games/minecraft"], "worlds")
        self.assertEqual(volumes["/app/data"], "database")


if __name__ == "__main__":
    unittest.main()
