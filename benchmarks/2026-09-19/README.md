# Attempt 07 optimization journal

Machine: macOS arm64. Full input: 13,809,692,168 bytes. Installed SDK/runtime baseline: 10.0.102 / 10.0.2; candidate: 11.0.100-rc.1.26425.128 / 11.0.0-rc.1.26425.128. .NET 11 is a release candidate.

## Method

`bench.py` starts fresh Release processes, alternates order, records Solve and wall times and canonical output hashes in `results.jsonl`. Five observations per variant unless specified. No cache flush: warm filesystem cache, startup/JIT included in Solve, build excluded. Compare medians, check ranges, rerun small effects. No simultaneous benchmark processes. Timings apply to this machine and dataset, not universal rankings.

Existing working-tree edits (net10 targets, argument selection, earlier benchmarks) preceded this work. The runner and application target are extended; the generator and September 6 artifacts are preserved.

`verify.py` is an independent Python integer oracle, including exact midpoint rounding toward positive infinity, ordinal UTF-16 name sorting, UTF-8 names, equal-prefix collisions, 10,000 stations, CRLF, optional BOM, missing final newline, and worker/buffer boundaries. Historical attempt 06 is a performance control, not a correctness oracle.

## Source review and hypotheses

- [Original article](https://michael-mckenna.com/blog/csharp-dot-net-1brc/): parallel byte parsing and integer aggregation provide the starting architecture.
- [.NET 9 performance](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/): JIT/inlining and span search; alternate dictionary lookup is interesting but the input is UTF-8 and the hot table already avoids strings.
- [.NET 10 performance](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/): bounds-check elimination, deabstraction and span/search improvements benefit straightforward span code. Multi-string SearchValues is unnecessary for one byte delimiter.
- [.NET 11 performance](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-11/): ARM64 compare-mask/bit-count and search code generation make SIMD delimiter search worth testing. Additional JIT simplifications may help unchanged code; measure before attributing gains.
- [.NET 11 runtime notes](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview7/runtime.md): runtime async is not expected to help synchronous parsing; only one join awaits tasks. New vector interleave APIs have no clear use here. Do not introduce unrelated APIs solely because they are new.

## Experiments

Results appended with each individual change. Rejected changes are reverted and their measurements remain in history.

### 00 — vanilla SDK/runtime upgrade

Unchanged attempt 06: .NET 10 median **3,904 ms** (3,813–4,185); .NET 11 median **3,880 ms** (3,779–4,814). Approximately 0.6% faster, within noise; no convincing performance gain from upgrade alone. Initial series overlapped a small correctness run/build, so final validation repeats this control without those activities. All outputs identical. Baselines built in isolated `/tmp/1brc-baseline-net{10,11}.0` copies with exact SDK global.json pins; only TargetFramework changed. Historical HEAD targeted net8, but the user's starting working tree already targeted net10.

### 01 — correctness-first attempt 07

Copied attempt 06, added full byte-name equality and dynamic table growth; fixed BOM placement, CRLF, ordinal ordering, invariant formatting and 1BRC midpoint rounding. Added `--file`/`--quiet`, default candidate 07, and independent regression fixtures. Attempt 06 unchanged. Three runs: control median **4,007 ms**, candidate **4,184 ms** (+4.4%); candidate range 4,088–4,185. All full-file station aggregates match the control (canonical hash ignores ordering). Correctness suite passes; extra name equality and capacity checks have a modest cost. Retain correctness even where it costs throughput.

### 02 — span delimiter scanning

EventPipe sampled-thread-time profile (`profile-before.log`) attributes 59.28% exclusive samples to ParseCompleteLines, 15.94% to PRead, 23.65% to waits. These are sampled thread-time percentages, **not hardware CPU samples**. Inlining may charge table work to the parser. Collected via `.tools/dotnet-trace collect --profile dotnet-sampled-thread-time -o /tmp/attempt07-before.nettrace -- dotnet 1brc/bin/Release/net11.0/1brc.dll 07`; diagnostic sockets require sandbox escalation. Raw traces kept in /tmp, report retained.

Replace per-byte parser state with span IndexOf for newline/separator and one shared line parser. Prior median **4,170 ms**, new **4,151 ms** (4,124–4,341): neutral within noise. Retain as a simpler foundation (removes duplicate parsing logic and 72 lines), not as a claimed performance win. All oracle fixtures and full-file aggregates pass.

### 03 — high-bit multiplicative table indexing

Use the high log2(capacity) bits of the multiplicative hash, so short names do not systematically share low-bit buckets. Prior **4,121 ms**, candidate **4,068 ms** (4,043–4,142), about 1.3% improvement but overlapping ranges. Retain improved distribution rather than claim a large win. All tests pass. Machine exposes 11 logical processors.

### 04 — fixed-format temperature parsing

Find the last complete newline once per buffer; each row searches only for its separator, then reads sign, one/two integer digits and one fractional digit directly. Preserve CRLF and scalar final-line handling. Prior median **4,079 ms**, candidate **3,841 ms** (3,825–3,846): **5.8% faster** with non-overlapping ranges. All fixtures/full-file aggregates pass. Retained.

### 05 — word-sized full-name hashing

Replace seven scalar byte shifts with 8-/4-byte little-endian reads and rotate/xor over the whole name. Full-name equality remains mandatory. Prior **3,920 ms**, candidate **3,852 ms** (3,834–3,936): small 1.7% median gain with overlap. Retain for full-name hash distribution as well as the observed gain. All fixtures/full-file aggregates pass.

### 06 — dynamic work distribution (rejected)

Sixteen ranges per worker, claimed via Interlocked.Increment, reusing each worker's table and buffer. Prior median **3,705 ms**, dynamic **3,893 ms** (+5.1%, range 3,805–4,005). More file opens and partial reads/internal FileStream buffers may offset load balancing. Reverted; exact experimental diff in `rejected/06-dynamic-ranges.patch`. Full-file oracle matches exactly: **1,001,000,000 rows, 413 stations** (the file is slightly larger than one billion rows). Added sum-overflow and shared-head/tail tests. Independent oracle source and result checksum retained.

### 07 — read-only memory mapping (rejected)

Pin one mapped view per worker; feed bounded 4 MiB spans without managed file-buffer copies. Pointer lifetime protected by try/finally, EOF/BOM tests pass. Prior **3,887 ms**, mapped **12,049 ms** (10,971–12,593), a severe regression on this macOS setup. No claim about the underlying page-fault cause without native profiling. Reverted including AllowUnsafeBlocks; patch retained. Buffered reads remain preferable here.
