"""Download the files from a rodin_get_result JSON (read from stdin) into a folder.

Usage: python3 -I fetch_rodin.py <dest_dir> [name ...] < result.json
With names, only files whose name matches are fetched (e.g. base_basic_pbr.glb texture_emissive.png).
"""
import json, os, sys, urllib.request

dest = sys.argv[1]
wanted = set(sys.argv[2:])
os.makedirs(dest, exist_ok=True)
data = json.load(sys.stdin)
seen = set()
for f in data['files']:
    name = f['name']
    if name in seen or (wanted and name not in wanted):
        continue
    seen.add(name)
    path = os.path.join(dest, name)
    with urllib.request.urlopen(f['url']) as r, open(path, 'wb') as out:
        out.write(r.read())
    print(name, os.path.getsize(path))
