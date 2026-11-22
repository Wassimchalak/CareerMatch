#!/usr/bin/env python3
"""Non-mutating HTTP integration checks against a running local/staging API.

Test only a service you own. No credentials are required or logged.
"""
import argparse
import sys
import urllib.error
import urllib.request

p = argparse.ArgumentParser()
p.add_argument("--base-url", default="http://localhost:5032")
p.add_argument("--production", action="store_true")
a = p.parse_args()
base = a.base_url.rstrip("/")
if not (base.startswith("http://localhost:") or base.startswith("http://127.0.0.1:") or base.startswith("https://")):
    p.error("Use localhost or HTTPS")
tests = [
    ("health", "/health", 200),
    ("anonymous saved jobs", "/api/SavedJob/mine", 401),
    ("anonymous application history", "/api/JobApplication/mine", 401)
]
if a.production:
    tests.extend([
        ("production Swagger", "/swagger/index.html", 404),
        ("production OpenAPI", "/openapi/v1.json", 404)
    ])
failed = 0
for name, path, expected in tests:
    try:
        with urllib.request.urlopen(base + path, timeout=12) as response:
            code = response.status
    except urllib.error.HTTPError as error:
        code = error.code
    except Exception as error:
        print("INCONCLUSIVE", name, type(error).__name__, str(error))
        failed += 1
        continue
    ok = code == expected
    print("PASS" if ok else "FAIL", name, code, "expected", expected)
    failed += not ok
sys.exit(1 if failed else 0)
