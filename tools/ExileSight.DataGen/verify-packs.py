"""Verify generated packs against their exact input bytes before bundling."""
import base64
import hashlib
import json
import sys
import shutil
from pathlib import Path

root, source, sha = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3].lower()


def require(condition, message):
    if not condition:
        raise SystemExit(message)


inputs = {
    f"data/{name}": hashlib.sha256((source / name).read_bytes()).hexdigest()
    for name in ("base_items.json", "mods.json", "stat_translations.json")
}
expected = {f"poe1-{kind}{suffix}" for kind in ("bases", "mods", "stats")
            for suffix in (".json", ".manifest.json", ".pack.json")}
expected.add("ATTRIBUTION.txt")
sums = {}
for line in (root / "SHA256SUMS").read_text().splitlines():
    digest, name = line.split("  ")
    require(name in expected and name not in sums, "Unexpected checksum entry")
    require(hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, "Checksum mismatch")
    sums[name] = digest
require(set(sums) == expected, "Missing checksum entries")
for kind in ("bases", "mods", "stats"):
    name = f"poe1-{kind}"
    raw = (root / f"{name}.json").read_bytes()
    content = json.loads(raw)
    manifest = json.loads((root / f"{name}.manifest.json").read_bytes())
    cache = json.loads((root / f"{name}.pack.json").read_bytes())
    require(manifest["source"] == {
        "repo": "repoe-fork/repoe-fork.github.io", "sha": sha, "files": inputs
    }, "Invalid provenance")
    require(manifest["contentSha256"] == hashlib.sha256(raw).hexdigest(), "Content hash mismatch")
    for document in (manifest, content):
        require(document["packId"] == f"poe1/{kind}" and document["game"] == 1
                and document["schemaVersion"] == 3 and document["packVersion"] == sha,
                "Pack identity/version mismatch")
    require(cache["manifest"] == manifest
            and base64.b64decode(cache["contentBytes"], validate=True) == raw, "Invalid cache envelope")
    require(f"RePoE fork project (commit {sha})" in manifest["attribution"]
            and "© Grinding Gear Games" in manifest["attribution"]
            and "permission of the project owner" in manifest["attribution"]
            and "not affiliated with or endorsed by Grinding Gear Games" in manifest["attribution"],
            "Missing attribution")
    require(len(raw) <= 32 * 1024 * 1024, "Content exceeds PackLoader limit")
notice = (root / "ATTRIBUTION.txt").read_text(encoding="utf-8")
require(notice == manifest["attribution"] + "\n", "Missing or incorrect human-readable attribution")
if len(sys.argv) == 5:
    destination = Path(sys.argv[4])
    destination.mkdir(parents=True, exist_ok=True)
    require(not any(destination.iterdir()), "Fallback destination must be empty")
    names = sorted(name for name in expected if name.endswith(".pack.json") or name == "ATTRIBUTION.txt")
    for name in names:
        shutil.copyfile(root / name, destination / name)
    (destination / "SHA256SUMS").write_text(
        "".join(f"{sums[name]}  {name}\n" for name in names), encoding="utf-8", newline="\n")
print("Verified bases, mods and stats: checksums, provenance, schema, envelopes and attribution.")
