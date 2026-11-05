"""Validate seeded login and Discord access on the disposable CI deployment."""
import json
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

settings = dict(line.split("=", 1) for line in Path(".env").read_text().splitlines()
                if line and not line.startswith("#") and "=" in line)
base = "http://127.0.0.1:" + settings.get("WEB_PORT", "6070")


def request(path, payload=None, token=None):
    headers = {"Content-Type": "application/json", "Origin": base}
    if token:
        headers["Cookie"] = "auth_token=" + token
    body = json.dumps(payload).encode() if payload is not None else None
    try:
        with urlopen(Request(base + path, data=body, headers=headers), timeout=30) as response:
            return response.status, json.load(response)
    except HTTPError as error:
        return error.code, None


status, _ = request("/api/discord/configuration")
assert status == 401, "Discord configuration must require login"
status, _ = request("/api/auth/login", {"username": settings["Auth__SeedUsername"],
                                       "password": "incorrect-password"})
assert status == 401, "Incorrect password must be rejected"
status, login = request("/api/auth/login", {"username": settings["Auth__SeedUsername"],
                                           "password": settings["Auth__SeedPassword"]})
assert status == 200 and login.get("accessToken"), "Seeded admin login failed"
assert login["role"].lower() == "admin", "Seeded user must be admin"
for path in ("/api/discord/configuration", "/api/discord/events"):
    status, _ = request(path, token=login["accessToken"])
    assert status == 200, "Authenticated Discord endpoint unavailable: " + path
print("Port, seeded administrator and authenticated Discord endpoints: OK")
