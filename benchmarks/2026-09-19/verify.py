"""Independent integer oracle. Exercises collisions, UTF-8, growth, ranges and buffers."""
import os, random, subprocess, tempfile
from pathlib import Path
DLL=Path(__file__).resolve().parents[2]/'1brc/bin/Release/net11.0/1brc.dll'
def verify(label, rows, ending=b'\n', bom=False, final=True):
    expected={}
    for name,value in rows:
        if name not in expected: expected[name]=[value,value,value,1]
        else:
            a=expected[name]; a[0]=min(a[0],value);a[1]=max(a[1],value);a[2]+=value;a[3]+=1
    def fmt(n): return ('-' if n<0 else '')+f'{abs(n)//10}.{abs(n)%10}'
    items=[]
    for name,(low,high,total,count) in expected.items():
        mean=(2*total+count)//(2*count)
        items.append(f'{name}={fmt(low)}/{fmt(mean)}/{fmt(high)}')
    items.sort(key=lambda s:s.split('=')[0].encode('utf-16-be'))
    want='{'+', '.join(items)+'}'
    payload=ending.join((name+';'+fmt(value)).encode() for name,value in rows)
    if final and rows: payload+=ending
    if bom: payload=b'\xef\xbb\xbf'+payload
    with tempfile.TemporaryDirectory() as d:
        path=Path(d)/'measurements.txt';path.write_bytes(payload)
        result=subprocess.run(['dotnet',str(DLL),'07','--file',str(path)],capture_output=True,text=True,timeout=60,check=True)
    got=result.stdout.split('\n\n')[0].strip()
    assert got==want,(label,got[:500],want[:500])
    print('PASS',label,len(rows),'rows',len(payload),'bytes',flush=True)
verify('empty',[])
verify('bom-only',[],bom=True)
verify('single-final',[('Å',-999)],bom=True,final=False)
verify('rounding',[(n,v) for n,vs in [('positive',[0,1]),('negative',[-1,0]),('negative2',[-2,-1])] for v in vs])
verify('prefix-collisions', [('abcdefgA',-999),('abcdefgB',999),('abcdefgA',11),('abcdefgB',-11)])
verify('CRLF-unicode', [('東京',10),('é',-12),('😀',30),('\ufeffinside',1),('Ａname',33)],ending=b'\r\n',bom=True,final=False)
verify('10000-stations',[(f'station{i:05}',i%1999-999) for i in range(10000)])
r=random.Random(8128)
names=['a','b','abcdefgA','abcdefgB','東京','é','z'*100,'Ａ','😀']
verify('random',[(r.choice(names),r.randrange(-999,1000)) for _ in range(50000)],final=False)
# > 4 MiB per worker with one worker: carry copies and BOM-like UTF-8 at chunk starts.
old=os.environ.get('DOTNET_PROCESSOR_COUNT');os.environ['DOTNET_PROCESSOR_COUNT']='1'
verify('buffer-boundaries',[(r.choice(names),r.randrange(-999,1000)) for _ in range(300000)],bom=True)
if old is None: del os.environ['DOTNET_PROCESSOR_COUNT']
else: os.environ['DOTNET_PROCESSOR_COUNT']=old
verify('worker-boundaries',[(r.choice(names),r.randrange(-999,1000)) for _ in range(120000)],ending=b'\r\n',final=False)
verify('64-bit-sum',[('hot',999)]*2200000)
verify('shared-head-and-tail',[(f'abcdefgh{i:05}abcdefgh',i%1999-999) for i in range(10000)])
# Equal rotate/xor word hashes, distinct complete names. Hash equality must not merge them.
verify('full-hash-collision',[('abcdefghA',-999),('`bcdefgha',999),('abcdefghA',10),('`bcdefgha',-10)])
# Exercise every separator lane and the scalar (< 8 remaining bytes) scanner tail.
for length in range(1,33):
    verify(f'name-length-{length}', [('x'*length,12),('x'*length,-34)],final=length%2==0)
