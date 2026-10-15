#!/bin/bash
cd /d/Unity/KeystoneKapers
unity command run_script --file AgentScripts/Scenarios.cs --entry Scenarios.$1 --timeout_ms ${2:-120000} --timeout 200 --format json | python -c "import sys,json; d=json.load(sys.stdin); r=d.get('data',{}).get('result',{}) if d.get('success') else d; print(r.get('result') if isinstance(r,dict) and r.get('result') else json.dumps(r,indent=1)[:3000])"
