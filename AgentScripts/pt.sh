#!/bin/bash
# usage: pt.sh Method '[args]'
cd /d/Unity/KeystoneKapers
m=$1; a=${2:-[]}
unity command run_script --file AgentScripts/PlayTest.cs --entry PlayTest.$m --args "$a" --format json | python -c "import sys,json; d=json.load(sys.stdin); r=d.get('data',{}).get('result',{}) if d.get('success') else d; print(r.get('result') if isinstance(r,dict) and 'result' in r else json.dumps(r,indent=1)[:2000])"
