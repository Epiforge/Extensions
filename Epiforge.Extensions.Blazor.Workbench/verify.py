#!/usr/bin/env python3
"""Runs the workbench, drives its verification page in headless Chromium, and writes what it found to a file.

Usage: python3 verify.py [results-file]

The results file defaults to TestResults/workbench-verification.json at the root of the repository. The script exits with
a non-zero status when any scenario fails or the page cannot be driven.
"""

import json
import os
import pathlib
import subprocess
import sys
import time
import urllib.request

from playwright.sync_api import sync_playwright

here = pathlib.Path(__file__).resolve().parent
results_path = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else here.parent / "TestResults" / "workbench-verification.json"
results_path.parent.mkdir(parents=True, exist_ok=True)
log_path = results_path.with_suffix(".log")
url = "http://127.0.0.1:5187"


def wait_for_server(server, deadline):
    while time.monotonic() < deadline:
        if server.poll() is not None:
            raise RuntimeError(f"the workbench exited with status {server.returncode}; see {log_path}")
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                if response.status == 200:
                    return
        except OSError:
            pass
        time.sleep(1)
    raise RuntimeError(f"the workbench did not answer at {url}; see {log_path}")


def main():
    report = {"url": url, "scenarios": [], "error": None}
    with open(log_path, "w") as log:
        server = subprocess.Popen(
            ["dotnet", "run", "-c", "Release", "--no-launch-profile", "--urls", url],
            cwd=here, stdout=log, stderr=subprocess.STDOUT,
            env={**os.environ, "ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
        try:
            wait_for_server(server, time.monotonic() + 300)
            with sync_playwright() as playwright:
                browser = playwright.chromium.launch()
                try:
                    page = browser.new_page()
                    page.goto(f"{url}/verify")
                    page.click("#run", timeout=60000)
                    page.wait_for_selector('#summary[data-complete="true"]', timeout=120000)
                    for row in page.query_selector_all("#results tbody tr"):
                        cells = row.query_selector_all("td")
                        report["scenarios"].append({
                            "name": row.get_attribute("data-scenario"),
                            "verdict": row.get_attribute("data-verdict"),
                            "detail": cells[2].inner_text() if len(cells) > 2 else ""})
                finally:
                    browser.close()
        except Exception as exception:
            report["error"] = f"{type(exception).__name__}: {exception}"
        finally:
            server.terminate()
            try:
                server.wait(timeout=30)
            except subprocess.TimeoutExpired:
                server.kill()
    failed = [scenario for scenario in report["scenarios"] if scenario["verdict"] != "pass"]
    report["passed"] = len(report["scenarios"]) - len(failed)
    report["failed"] = len(failed)
    results_path.write_text(json.dumps(report, indent=2))
    print(f"{report['passed']} of {len(report['scenarios'])} scenarios passed; results in {results_path}")
    if report["error"]:
        print(report["error"])
    return 1 if failed or report["error"] or not report["scenarios"] else 0


if __name__ == "__main__":
    sys.exit(main())
