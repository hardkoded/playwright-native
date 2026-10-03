#!/usr/bin/env bash
# Assign test classes to a CI shard and export TEST_FILTER for dotnet test.
# Requires: SHARD_INDEX, SHARD_TOTAL, GITHUB_ENV, RUNNER_TEMP.
set -euo pipefail

if [[ -z "${SHARD_INDEX:-}" || -z "${SHARD_TOTAL:-}" ]]; then
  echo "SHARD_INDEX and SHARD_TOTAL are required" >&2
  exit 1
fi

if [[ -z "${GITHUB_ENV:-}" || -z "${RUNNER_TEMP:-}" ]]; then
  echo "GITHUB_ENV and RUNNER_TEMP are required" >&2
  exit 1
fi

# List every test's fully-qualified name straight from the built
# assembly (dotnet test --list-tests only prints the bare method
# name, which collides across classes and isn't safe to filter on).
# $RUNNER_TEMP (not /tmp) so the path is already OS-native on Windows.
all_tests="$RUNNER_TEMP/all_tests.txt"
dotnet vstest src/PlaywrightNative.Tests/bin/Release/net10.0/PlaywrightNative.Tests.dll \
  /ListFullyQualifiedTests "/ListTestsTargetPath:$all_tests"

# Drop the trailing ".MethodName" (and any TestCase "(...)" suffix)
# to get one entry per test class, then assign classes to shards by
# position in a stable sort -- deterministic across the shardTotal
# jobs of a single run without the shards needing to coordinate.
all_classes="$RUNNER_TEMP/all_classes.txt"
sed -E 's/\.[A-Za-z0-9_]+(\([^)]*\))?$//' "$all_tests" | sort -u > "$all_classes"
echo "Discovered $(wc -l < "$all_classes") test classes"

shard_classes="$RUNNER_TEMP/shard_classes.txt"
awk -v shard="$SHARD_INDEX" -v total="$SHARD_TOTAL" \
  'NR % total == (shard - 1) { print }' "$all_classes" > "$shard_classes"
echo "Shard ${SHARD_INDEX}/${SHARD_TOTAL}: $(wc -l < "$shard_classes") classes"

filter=$(awk '{ printf "%sFullyQualifiedName~%s.", (NR > 1 ? "|" : ""), $0 }' "$shard_classes")
echo "TEST_FILTER=$filter" >> "$GITHUB_ENV"
