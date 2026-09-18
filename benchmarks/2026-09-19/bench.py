"""Sequential alternating full-file runs; subprocess startup excluded from solve_ms.
Usage: python3 bench.py LABEL DLL:ATTEMPT [DLL:ATTEMPT ...] --runs 5
Each process includes JIT warmup. No cache flushing; compare warm filesystem cache.
"""
import argparse, hashlib, json, os, re, statistics, subprocess, time
from pathlib import Path
p=argparse.ArgumentParser(); p.add_argument('label'); p.add_argument('targets',nargs='+'); p.add_argument('--runs',type=int,default=5)
a=p.parse_args(); rows=[]
for i in range(a.runs):
    targets=a.targets if i%2==0 else a.targets[::-1]
    for target in targets:
        dll,attempt=target.rsplit(':',1); start=time.perf_counter()
        r=subprocess.run(['dotnet',dll,attempt],capture_output=True,text=True,check=True)
        wall=time.perf_counter()-start
        output=r.stdout.split('\n\n')[0].strip()
        output='\n'.join(sorted(output.strip('{}').split(', ')))
        ms=int(re.search(r'total: (\d+)ms',r.stdout)[1])
        row=dict(label=a.label,iteration=i,target=target,solve_ms=ms,wall_s=wall,sha256=hashlib.sha256(output.encode()).hexdigest())
        rows.append(row); print(json.dumps(row),flush=True)
with (Path(__file__).parent/'results.jsonl').open('a') as f:
    for row in rows: f.write(json.dumps(row)+'\n')
for target in a.targets:
    values=[r['solve_ms'] for r in rows if r['target']==target]
    print(target,'median',statistics.median(values),'min',min(values),'max',max(values))
assert len({r['sha256'] for r in rows})==1, 'Output mismatch; inspect correctness before accepting timings'
