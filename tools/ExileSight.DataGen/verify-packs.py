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
kinds = ("bases", "mods", "stats", "guide", "mapmods", "events", "leagues")
config = json.loads(Path(__file__).with_name("repoe.json").read_text())
exile = config["exileUiSha"]
notices = []
expected = {f"poe1-{kind}{suffix}" for kind in kinds
            for suffix in (".json", ".manifest.json", ".pack.json")}
expected.add("ATTRIBUTION.txt")
sums = {}
for line in (root / "SHA256SUMS").read_text().splitlines():
    digest, name = line.split("  ")
    require(name in expected and name not in sums, "Unexpected checksum entry")
    require(hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, "Checksum mismatch")
    sums[name] = digest
require(set(sums) == expected, "Missing checksum entries")
for kind in kinds:
    name = f"poe1-{kind}"
    raw = (root / f"{name}.json").read_bytes()
    content = json.loads(raw)
    manifest = json.loads((root / f"{name}.manifest.json").read_bytes())
    cache = json.loads((root / f"{name}.pack.json").read_bytes())
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()
    if kind in ("bases", "mods", "stats"):
        provenance = {"repo": "repoe-fork/repoe-fork.github.io", "sha": sha, "files": inputs}
        version = sha
    else:
        authored = Path(__file__).parent
        events_hash, leagues_hash = (digest(authored / f"{name}.json") for name in ("events", "leagues"))
        version = hashlib.sha256((sha + exile + events_hash + leagues_hash).encode()).hexdigest()
        if kind in ("guide", "mapmods"):
            files = {f"https://raw.githubusercontent.com/Lailloken/Exile-UI/{exile}/data/english/"
                     + ("[leveltracker] default guide.json" if kind == "guide" else "map-info.txt"):
                     digest(source / ("guide.json" if kind == "guide" else "map-info.txt"))}
            if kind == "guide":
                files.update({f"https://raw.githubusercontent.com/repoe-fork/repoe-fork.github.io/{sha}/data/{name}":
                              digest(source / name) for name in ("world_areas.json", "gems.json")})
                require("HeartofPhos/exile-leveling" in manifest["attribution"], "Missing guide attribution")
            require("MIT License" in manifest["attribution"], "Missing MIT notice")
            provenance = {"repo": "Lailloken/Exile-UI", "sha": exile, "files": files}
        else:
            hashed = digest(authored / f"{kind}.json")
            provenance = {"repo": "ExileSight authored data", "sha": hashed,
                          "files": {f"tools/ExileSight.DataGen/{kind}.json": hashed}}
        field = {"mapmods": "mapMods"}.get(kind, kind)
        require(bool(content.get(field)), "Missing pack content")
    require(manifest["source"] == provenance, "Invalid provenance")
    require(manifest["contentSha256"] == hashlib.sha256(raw).hexdigest(), "Content hash mismatch")
    for document in (manifest, content):
        require(document["packId"] == f"poe1/{kind}" and document["game"] == 1
                and document["schemaVersion"] == 3 and document["packVersion"] == version,
                "Pack identity/version mismatch")
    require(cache["manifest"] == manifest
            and base64.b64decode(cache["contentBytes"], validate=True) == raw, "Invalid cache envelope")
    if kind in ("bases", "mods", "stats", "guide"):
        require(f"RePoE fork project (commit {sha})" in manifest["attribution"]
                and "© Grinding Gear Games" in manifest["attribution"]
                and "permission of the project owner" in manifest["attribution"]
                and "not affiliated with or endorsed by Grinding Gear Games" in manifest["attribution"]
                and "D-047" not in manifest["attribution"],
                "Missing attribution")
    if manifest["attribution"] not in notices:
        notices.append(manifest["attribution"])
    require(len(raw) <= 32 * 1024 * 1024, "Content exceeds PackLoader limit")
notice = (root / "ATTRIBUTION.txt").read_text(encoding="utf-8")
require(notice == "\n".join(notices) + "\n", "Missing or incorrect human-readable attribution")
if len(sys.argv) == 5:
    destination = Path(sys.argv[4])
    destination.mkdir(parents=True, exist_ok=True)
    require(not any(destination.iterdir()), "Fallback destination must be empty")
    names = sorted(name for name in expected if name.endswith(".pack.json") or name == "ATTRIBUTION.txt")
    for name in names:
        shutil.copyfile(root / name, destination / name)
    (destination / "SHA256SUMS").write_text(
        "".join(f"{sums[name]}  {name}\n" for name in names), encoding="utf-8", newline="\n")
print("Verified seven packs: checksums, provenance, schema, envelopes and attribution.")
