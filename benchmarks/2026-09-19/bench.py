"""Sequential alternating full-file runs; subprocess startup excluded from solve_ms.
Usage: python3 bench.py LABEL DLL:ATTEMPT [DLL:ATTEMPT ...] --runs 5
Each process includes JIT warmup. No cache flushing; compare warm filesystem cache.
"""
import argparse, hashlib, json, os, re, resource, statistics, subprocess, time
from pathlib import Path
p=argparse.ArgumentParser(); p.add_argument('label'); p.add_argument('targets',nargs='+'); p.add_argument('--runs',type=int,default=5); p.add_argument('--file')
a=p.parse_args(); rows=[]
for i in range(a.runs):
    targets=a.targets if i%2==0 else a.targets[::-1]
    for target in targets:
        dll,variant=target.rsplit(':',1); attempt,_,workers=variant.partition('@')
        env=os.environ.copy()
        if workers: env['DOTNET_PROCESSOR_COUNT']=workers
        command=(['dotnet',dll] if dll.endswith('.dll') else [dll])+[attempt]
        if a.file: command += ['--file',a.file]
        usage=resource.getrusage(resource.RUSAGE_CHILDREN)
        start=time.perf_counter()
        r=subprocess.run(command,capture_output=True,text=True,check=True,env=env)
        wall=time.perf_counter()-start
        after=resource.getrusage(resource.RUSAGE_CHILDREN)
        output=r.stdout.split('\n\n')[0].strip()
        output='\n'.join(sorted(output.strip('{}').split(', ')))
        ms=int(re.search(r'total: (\d+)ms',r.stdout)[1])
        row=dict(label=a.label,iteration=i,target=target,file=a.file or "measurements.txt",solve_ms=ms,wall_s=wall,user_s=after.ru_utime-usage.ru_utime,system_s=after.ru_stime-usage.ru_stime,sha256=hashlib.sha256(output.encode()).hexdigest())
        rows.append(row); print(json.dumps(row),flush=True)
with (Path(__file__).parent/'results.jsonl').open('a') as f:
    for row in rows: f.write(json.dumps(row)+'\n')
for target in a.targets:
    values=[r['solve_ms'] for r in rows if r['target']==target]
    print(target,'median',statistics.median(values),'min',min(values),'max',max(values))
assert len({r['sha256'] for r in rows})==1, 'Output mismatch; inspect correctness before accepting timings'
