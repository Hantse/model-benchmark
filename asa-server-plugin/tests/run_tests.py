"""Run the protected CTest suite and verify its complete, fixed population."""
from pathlib import Path
import json
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
EXPECTED = (ROOT / "tests/cases.txt").read_text(encoding="utf-8").splitlines()
MAX_FAILURE_BYTES = 16 * 1024


def enrich_ctest_failures(tree):
    """Keep CTest outcomes and stdout while exposing assertions to JUnit readers."""
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
    report = ROOT / "TestResults/acceptance.xml"
    report.parent.mkdir(exist_ok=True)
    report.unlink(missing_ok=True)
    inventory = subprocess.run(
        ["ctest", "--test-dir", str(ROOT / "obj"), "-C", "Release", "--show-only=json-v1"],
        cwd=ROOT, timeout=30, check=True, capture_output=True, text=True,
    )
    registered = [test["name"] for test in json.loads(inventory.stdout)["tests"]]
    if len(EXPECTED) != 36 or sorted(registered) != sorted(EXPECTED):
        raise RuntimeError("CTest inventory does not match the 36 protected identities")
    completed = subprocess.run(
        ["ctest", "--test-dir", str(ROOT / "obj"), "-C", "Release",
         "--output-on-failure", "--output-junit", str(report)], cwd=ROOT,
        timeout=480, check=False,
    )
    if not report.is_file():
        raise RuntimeError("CTest did not produce its required JUnit report")
    if completed.returncode not in (0, 8):
        raise RuntimeError("CTest did not finish with a normal pass or test-failure exit")
    tree = ET.parse(report)
    cases = list(tree.iter("testcase"))
    names = [case.get("name") for case in cases]
    if len(EXPECTED) != 36 or len(names) != len(EXPECTED) or sorted(names) != sorted(EXPECTED):
        raise RuntimeError("CTest acceptance discovery does not match the 36 protected identities")
    if any(case.find("skipped") is not None for case in cases):
        raise RuntimeError("Mandatory CTest cases must not be skipped")
    failed = any(case.find("failure") is not None or case.find("error") is not None for case in cases)
    if (completed.returncode == 0) == failed:
        raise RuntimeError("CTest exit code contradicts its JUnit report")
    if enrich_ctest_failures(tree):
        tree.write(report, encoding="utf-8", xml_declaration=True)
    print(f"BENCHMARK_TEST_RUN_COMPLETE {len(cases)}", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
