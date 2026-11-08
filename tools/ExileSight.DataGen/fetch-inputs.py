"""Fetch build inputs only from fixed GitHub repositories at full commit pins."""
import json
import re
import sys
import urllib.request
from pathlib import Path
from urllib.parse import quote

config = json.loads(Path(__file__).with_name('repoe.json').read_text())
root = Path(sys.argv[1])
sha = sys.argv[2] if len(sys.argv) > 2 else config['sourceSha']
exile = config['exileUiSha']
if not all(re.fullmatch('[0-9a-f]{40}', pin) for pin in (sha, exile)):
    raise SystemExit('Full lowercase commit pins required')

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise RuntimeError('Redirect refused')

opener = urllib.request.build_opener(NoRedirect)
root.mkdir(parents=True, exist_ok=True)
inputs = {name: f'repoe-fork/repoe-fork.github.io/{sha}/data/{name}' for name in
          ('base_items.json', 'mods.json', 'stat_translations.json', 'world_areas.json', 'gems.json')}
inputs.update({
    'guide.json': f'Lailloken/Exile-UI/{exile}/data/english/[leveltracker] default guide.json',
    'map-info.txt': f'Lailloken/Exile-UI/{exile}/data/english/map-info.txt',
})
for name, path in inputs.items():
    request = urllib.request.Request('https://raw.githubusercontent.com/' + quote(path),
                                     headers={'User-Agent': 'ExileSight-DataGen/1.0'})
    with opener.open(request, timeout=120) as response:
        (root / name).write_bytes(response.read())
