"""Static probe model for generator names; candidate contains no dataset-specific keys."""
import json, re
from pathlib import Path
root=Path(__file__).resolve().parents[2]
names=re.findall(r'new WeatherStation\("([^"]+)"',(root/'CreateMeasurements/Program.cs').read_text())
mask=(1<<64)-1
keys=[]
for name in names:
    data=name.encode(); value=0
    for offset in range(0,len(data),8):
        value=(((value<<5)|(value>>59))&mask)^int.from_bytes(data[offset:offset+8],'little')
    keys.append(value^len(data))
rows=[]
for capacity in [1024,2048,4096,8192,16384,32768]:
    slots=[None]*capacity; probes=[]
    for name,key in zip(names,keys):
        index=((key*11400714819323198485)&mask)>>(64-(capacity.bit_length()-1)); count=1
        while slots[index] is not None:
            index=(index+1)&(capacity-1); count+=1
        slots[index]=name; probes.append(count)
    rows.append(dict(capacity=capacity,stations=len(names),average_successful_probes=sum(probes)/len(probes),maximum_probes=max(probes)))
(Path(__file__).parent/'probe-analysis.json').write_text(json.dumps(rows,indent=2)+'\n')
print(json.dumps(rows,indent=2))
