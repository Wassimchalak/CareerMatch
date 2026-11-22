#!/usr/bin/env python3
"""Record real endpoint latency under explicit authorization; no synthetic timings.

For local/staging only. Requires a test JWT in CAREERMATCH_TEST_JWT and JSON
request body. Repeated requests do not prove cache hits unless the server
independently confirms cache state. Use disposable accounts and API budgets.
"""
import argparse
import csv
import os
import time
import urllib.request
import urllib.error
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument("--base-url",required=True)
p.add_argument("--endpoint",default="/api/JobSearch/search")
p.add_argument("--payload",required=True,help="Path to real JSON request fixture")
p.add_argument("--runs",type=int,default=10)
p.add_argument("--output",default="results/api_timings.csv")
p.add_argument("--token-env",default="CAREERMATCH_TEST_JWT")
a=p.parse_args()
if not 2 <= a.runs <= 15:
    p.error("--runs must be between 2 and 15")
base=a.base_url.rstrip("/")
if not (base.startswith("http://localhost:") or base.startswith("http://127.0.0.1:") or base.startswith("https://")):
    p.error("Use localhost or HTTPS so bearer credentials cannot be intercepted")
if not a.endpoint.startswith("/") or a.endpoint.startswith("//"):
    p.error("Endpoint must be a relative API path")
token=os.getenv(a.token_env)
if not token:
    p.error("Set "+a.token_env+" securely; tokens are not logged")
payload=Path(a.payload).read_bytes()
results=[]
for run in range(1,a.runs+1):
    request=urllib.request.Request(base+a.endpoint,method="POST",data=payload,headers={
        "Authorization":"Bearer "+token,
        "Content-Type":"application/json"
    })
    start=time.perf_counter()
    try:
        with urllib.request.urlopen(request,timeout=150) as response:
            status=response.status
            byte_count=len(response.read())
    except urllib.error.HTTPError as error:
        status=error.code
        byte_count=len(error.read())
    except Exception as error:
        status="ERROR:"+type(error).__name__
        byte_count=0
    elapsed=round(1000*(time.perf_counter()-start),2)
    results.append({"run":run,"http_status":status,"elapsed_ms":elapsed,"response_bytes":byte_count,
                    "cache_state":"NOT_SERVER_VERIFIED"})
    print("run",run,"status",status,"elapsed_ms",elapsed)
output=Path(a.output)
output.parent.mkdir(parents=True,exist_ok=True)
with output.open("w",newline="",encoding="utf-8") as target:
    writer=csv.DictWriter(target,fieldnames=results[0])
    writer.writeheader()
    writer.writerows(results)
print("Wrote",output,"- interpret cold/warm only if cache state confirmed server-side")
