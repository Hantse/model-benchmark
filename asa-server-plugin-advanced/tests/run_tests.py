"""Validate all 108 genuine CTest/JUnit identities; never accept partial runs."""
from pathlib import Path
import json
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PREFIX = "cpp.asa_plugin_advanced."
EXPECTED = [PREFIX + name for name in
            (ROOT / "tests/cases.txt").read_text(encoding="utf-8").splitlines() if name]
MAX_FAILURE_BYTES = 16 * 1024


def enrich_ctest_failures(tree):
    """Expose CTest's genuine assertion text without changing any outcome."""
    changed = False
    for case in tree.iter("testcase"):
        output = "\n".join(element.text or "" for element in case.findall("system-out")).strip()
        if not output:
            continue
        for failure in list(case.findall("failure")) + list(case.findall("error")):
            original = failure.get("message", "")
            message = original + ("\n" if original else "") + output
            details = (failure.text or "") + ("\n" if failure.text else "") + output
            failure.set("message", message.encode("utf-8")[:MAX_FAILURE_BYTES].decode("utf-8", errors="ignore"))
            failure.text = details.encode("utf-8")[:MAX_FAILURE_BYTES].decode("utf-8", errors="ignore")
            changed = True
    return changed


def main():
    if len(EXPECTED) != 108 or len(set(EXPECTED)) != 108:
        raise RuntimeError("protected_test_inventory_invalid")
    command = ["ctest", "--test-dir", str(ROOT / "obj"), "-C", "Release"]
    discovery = subprocess.run(command + ["--show-only=json-v1"], check=True,
                               capture_output=True, text=True, timeout=30)
    tests = json.loads(discovery.stdout)["tests"]
    if sorted(test["name"] for test in tests) != sorted(EXPECTED):
        raise RuntimeError("ctest_discovery_mismatch")
    executables = {test["command"][0] for test in tests if test.get("command")}
    if len(executables) != 1 or any(len(test.get("command", [])) != 2 for test in tests):
        raise RuntimeError("ctest_command_inventory_mismatch")
    registered = subprocess.run([next(iter(executables)), "--list"], check=True,
                                capture_output=True, text=True, timeout=30)
    if sorted(PREFIX + name for name in registered.stdout.splitlines()) != sorted(EXPECTED):
        raise RuntimeError("executable_inventory_mismatch")
    destination = ROOT / "TestResults"
    destination.mkdir(exist_ok=True)
    report = destination / "cpp.xml"
    report.unlink(missing_ok=True)
    result = subprocess.run(command + ["--output-on-failure", "--output-junit", str(report)],
                            cwd=ROOT, timeout=480)
    if result.returncode not in (0, 8):
        raise RuntimeError("ctest_infrastructure_failure")
    tree = ET.parse(report)
    entries = list(tree.iter("testcase"))
    if sorted(entry.get("name") for entry in entries) != sorted(EXPECTED):
        raise RuntimeError("ctest_report_inventory_mismatch")
    if any(entry.find("skipped") is not None or entry.get("status", "").lower() in
           {"notrun", "disabled", "skipped"} for entry in entries):
        raise RuntimeError("mandatory_test_skipped")
    failed = sum(entry.find("failure") is not None or entry.find("error") is not None
                 for entry in entries)
    if (result.returncode == 0) != (failed == 0):
        raise RuntimeError("ctest_exit_report_mismatch")
    if enrich_ctest_failures(tree):
        tree.write(report, encoding="utf-8", xml_declaration=True)
    print("BENCHMARK_TEST_RUN_COMPLETE 108", flush=True)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
