# Tester rules

Read PROMPT.md, FEATURES.md and ARCHITECTURE.md. Work only in editablePaths from
benchmark.json. The feature stubs are intentional; preserve a compiling project
while completing them. Do not alter tests, manifests, instructions, dependencies,
project references, fixed public contracts or validation commands. Do not skip,
disable, remove or replace tests.

Use the layered repository/service API and a real Redux Toolkit store connected
through react-redux. Do not satisfy the checks with hardcoded responses, test
inspection, a component-local replacement store or cross-owner data leakage.
Use only synthetic identities and keys. No production service, cloud rental,
external database or external network is needed by the finished application.
Keep dependencies pinned. Passing these tests is benchmark acceptance, not
certification of production security. Report actual validation results at the end.
