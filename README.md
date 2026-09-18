# 1brc

C# experiments for the [One Billion Row Challenge](https://github.com/gunnarmorling/1brc).

Attempt **07** is the default candidate and targets **.NET 11** (tested with SDK `11.0.100-rc.1.26425.128`). Historical attempts, including 06, remain available for comparison.

```sh
dotnet build 1brc/1brc.csproj -c Release
dotnet 1brc/bin/Release/net11.0/1brc.dll 07 --file measurements.txt
```

Omit `--file` to use `measurements.txt` in the current directory. `--quiet` suppresses station output while retaining timing output. Pass attempt IDs such as `06 07` to run multiple attempts; use the benchmark script below for alternating separate-process comparisons.

Attempt 07 assumes valid 1BRC input: UTF-8 station names of 1–100 bytes without semicolons/newlines, up to 10,000 distinct stations, and temperatures from -99.9 to 99.9 in canonical `[-]d.d` or `[-]dd.d` notation. It additionally supports CRLF, a leading UTF-8 BOM, empty input, and a missing final newline. It aggregates integer tenths with 64-bit sums, resolves hash collisions with exact name equality, sorts ordinally, and rounds mean ties toward positive infinity. It is an optimized parser, not a general-purpose malformed-input validator.

## Verify and benchmark

```sh
python3 benchmarks/2026-09-19/verify.py
python3 benchmarks/2026-09-19/bench.py comparison \
  1brc/bin/Release/net11.0/1brc.dll:06 \
  1brc/bin/Release/net11.0/1brc.dll:07 --runs 5
```

The independent full-file oracle is a separate implementation:

```sh
dotnet run --project benchmarks/2026-09-19/oracle/Oracle.csproj -c Release -- measurements.txt
```

See the [optimization journal](benchmarks/2026-09-19/README.md) for every experiment, accepted and rejected patches, raw timings, profiling, .NET release-note review, and reproduction details. Benchmark results are specific to the machine, workload and host load; use paired runs on your own system.
