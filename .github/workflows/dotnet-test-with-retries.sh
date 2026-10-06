#!/usr/bin/env bash
# Run the test suite like upstream Playwright CI (retries: process.env.CI ? 3 : 0):
# re-run each failed test up to 3 more times, in a fresh test process. A test
# that passes on a retry is reported as flaky and does not fail the job.
# Usage: dotnet-test-with-retries.sh [command prefix, e.g. xvfb-run ... --]
# Reads TEST_FILTER (optional) and RUNNER_TEMP.
set -uo pipefail

RETRIES=3
# More failures than this is a real breakage, not flakiness: do not retry.
MAX_RETRIED_FAILURES=40

results_dir="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/test-retries"
rm -rf "$results_dir"
mkdir -p "$results_dir"
# On Windows "python3" can be a Store stub that does not run: use the first that works.
for python in python3 python; do
  "$python" -c '' 2>/dev/null && break
done

run_tests() {
  local attempt="$1" filter="$2"
  ${PREFIX[@]+"${PREFIX[@]}"} dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj \
    --no-build -c Release -f net10.0 -s src/PlaywrightNative.Tests/test.runsettings \
    --results-directory "$results_dir" --logger "trx;LogFileName=attempt-$attempt.trx" \
    ${filter:+--filter "$filter"}
}

# Prints the fully-qualified names of the failed tests in a TRX file.
failed_tests() {
  "$python" - "$1" <<'EOF'
import sys
import xml.etree.ElementTree as ET
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
root = ET.parse(sys.argv[1]).getroot()
methods = {}
for test in root.iterfind('t:TestDefinitions/t:UnitTest', ns):
    method = test.find('t:TestMethod', ns)
    methods[test.get('id')] = method.get('className') + '.' + method.get('name')
names = set()
for result in root.iterfind('t:Results/t:UnitTestResult', ns):
    if result.get('outcome') == 'Failed':
        names.add(methods[result.get('testId')])
print('\n'.join(sorted(names)))
EOF
}

# Builds a dotnet test filter that matches exactly the given tests.
# Parameterized tests ("Name(1)") match by their method name.
to_filter() {
  local filter="" name
  while IFS= read -r name; do
    [[ -z "$name" ]] && continue
    if [[ "$name" == *"("* ]]; then
      filter+="${filter:+|}FullyQualifiedName~${name%%(*}"
    else
      filter+="${filter:+|}FullyQualifiedName=$name"
    fi
  done
  printf '%s' "$filter"
}

PREFIX=("$@")
run_tests 0 "${TEST_FILTER:-}"
status=$?
[[ $status -eq 0 ]] && exit 0

failed=""
[[ -f "$results_dir/attempt-0.trx" ]] && failed=$(failed_tests "$results_dir/attempt-0.trx")
count=$(printf '%s' "$failed" | grep -c . || true)
if [[ $count -eq 0 || $count -gt $MAX_RETRIED_FAILURES ]]; then
  echo "Not retrying: $count failed test(s) found in the results."
  exit "$status"
fi
first_failed="$failed"

for attempt in $(seq 1 "$RETRIES"); do
  echo "::group::Retry #$attempt of $(printf '%s\n' "$failed" | grep -c .) failed test(s)"
  printf '%s\n' "$failed"
  retried="$failed"
  run_tests "$attempt" "$(printf '%s\n' "$retried" | to_filter)"
  status=$?
  echo "::endgroup::"
  if [[ -f "$results_dir/attempt-$attempt.trx" ]]; then
    failed=$(failed_tests "$results_dir/attempt-$attempt.trx")
  fi
  if [[ $status -eq 0 ]]; then
    failed=""
    break
  fi
  if [[ -z "$failed" ]]; then
    # The run failed without a failed test (for example a crash): stop here,
    # an empty filter would run the whole suite.
    failed="$retried"
    break
  fi
done

flaky=$(comm -23 <(printf '%s\n' "$first_failed" | sort) <(printf '%s\n' "$failed" | sort))
if [[ -n "$flaky" ]]; then
  echo "Flaky tests (failed, then passed on retry):"
  sed 's/^/  /' <<< "$flaky"
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    { echo "### Flaky tests"; sed 's/^/- /' <<< "$flaky"; } >> "$GITHUB_STEP_SUMMARY"
  fi
fi

if [[ -n "$failed" ]]; then
  echo "Still failing after retries:"
  sed 's/^/  /' <<< "$failed"
fi
exit "$status"
