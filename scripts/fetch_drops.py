#!/usr/bin/env python3
"""
Génère Data/drops.json depuis les données Garland Tools.

Garland Tools encode ses mob IDs ainsi :
    garlandId = zonePrefix * 10^10 + bNpcNameId
    → bNpcNameId = garlandId % 10_000_000_000

Usage :
    python fetch_drops.py                         # IDs 1–15000 (matériaux communs)
    python fetch_drops.py --range 1 30000         # Plage étendue
    python fetch_drops.py --ids 4576 5289 12345   # Items spécifiques
    python fetch_drops.py --out ../Data/drops.json
"""

import json
import time
import argparse
import requests
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from threading import Lock

GARLAND_URL   = "https://www.garlandtools.org/db/doc/item/en/3/{}.json"
MAP_URL   = "https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en/Map.csv"
PLACE_URL = "https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en/PlaceName.csv"

BNPC_MOD      = 10_000_000_000
CACHE_DIR     = Path("cache")
DEFAULT_RANGE = (1, 15_000)
WORKERS       = 20             # requêtes parallèles
_print_lock   = Lock()


# ─── Data helpers ─────────────────────────────────────────────────────────────

def load_csv_index(url: str, key_col: int, val_col: int) -> dict[int, str]:
    """Télécharge un CSV ffxiv-datamining (en/) et retourne {rowId: valeur}.
    Format : ligne 0 = header, ligne 1+ = données.
    """
    print(f"Téléchargement {url.split('/')[-1]}...")
    r = requests.get(url, timeout=30)
    r.raise_for_status()
    lines = r.text.splitlines()
    result: dict[int, str] = {}
    for line in lines[1:]:           # skip header
        parts = line.split(",")
        try:
            row_id = int(parts[key_col])
            value  = parts[val_col].strip().strip('"')
            if value:
                result[row_id] = value
        except (ValueError, IndexError):
            continue
    print(f"  {len(result)} entrées chargées.")
    return result


class ZoneInfo:
    __slots__ = ("territory_id", "name")
    def __init__(self, territory_id: int, name: str):
        self.territory_id = territory_id
        self.name = name


def load_zone_map() -> dict[int, ZoneInfo]:
    """
    Garland Tools utilise les Map IDs (Map.csv col 0) comme identifiants de zone.
    Map.csv :     col 0 = Map ID (= z dans Garland)
                  col 6 = PlaceName ref
                  col 8 = TerritoryType ID (celui utilisé par Dalamud)
    PlaceName.csv : col 0 = ID, col 1 = Name.
    Retourne : { garlandZoneId → ZoneInfo(territory_id, name) }
    """
    place_names = load_csv_index(PLACE_URL, 0, 1)

    print("Téléchargement Map.csv...")
    r = requests.get(MAP_URL, timeout=30)
    r.raise_for_status()
    lines = r.text.splitlines()

    zones: dict[int, ZoneInfo] = {}
    for line in lines[1:]:
        parts = line.split(",")
        try:
            map_id         = int(parts[0])
            place_name_ref = int(parts[6])
            territory_id   = int(parts[8])
            name = place_names.get(place_name_ref, "")
            if map_id > 0 and territory_id > 0 and name:
                zones[map_id] = ZoneInfo(territory_id, name)
        except (ValueError, IndexError):
            continue

    print(f"  {len(zones)} zones résolues.")
    return zones


# ─── Garland Tools ────────────────────────────────────────────────────────────

def fetch_item(item_id: int) -> tuple[int, dict | None]:
    cache_path = CACHE_DIR / f"{item_id}.json"

    if cache_path.exists():
        raw = cache_path.read_text(encoding="utf-8")
        return item_id, (json.loads(raw) if raw != "{}" else None)

    try:
        r = requests.get(GARLAND_URL.format(item_id), timeout=15)
        if r.status_code == 404:
            cache_path.write_text("{}")
            return item_id, None
        r.raise_for_status()
        data = r.json()
        cache_path.write_text(json.dumps(data, ensure_ascii=False))
        return item_id, data
    except Exception as e:
        with _print_lock:
            print(f"  [!] Item {item_id} : {e}")
        return item_id, None


def extract_sources(data: dict, zones: dict[int, ZoneInfo]) -> list[dict]:
    item     = data.get("item", {})
    mob_ids  = item.get("drops", [])
    if not mob_ids:
        return []

    partials  = data.get("partials", [])
    mobs_by_id = {
        p["obj"]["i"]: p["obj"]
        for p in partials
        if p.get("type") == "mob" and "i" in p.get("obj", {})
    }

    seen    : set[tuple] = set()
    sources : list[dict] = []

    for garland_id in mob_ids:
        mob = mobs_by_id.get(garland_id)
        if not mob:
            continue

        bnpc_name_id = int(garland_id) % BNPC_MOD
        garland_zone = int(mob.get("z", 0))
        zone_info    = zones.get(garland_zone)

        if bnpc_name_id == 0 or garland_zone == 0 or zone_info is None:
            continue

        key = (bnpc_name_id, zone_info.territory_id)
        if key in seen:
            continue
        seen.add(key)

        sources.append({
            "bNpcNameId":  bnpc_name_id,
            "mobName":     mob.get("n", "Unknown"),
            "territoryId": zone_info.territory_id,
            "zoneName":    zone_info.name,
            "positions":   [],   # Garland Tools ne fournit pas de coordonnées de spawn
            "dropRate":    1.0
        })

    return sorted(sources, key=lambda s: s["territoryId"])


# ─── Main ─────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Génère drops.json depuis Garland Tools.")
    parser.add_argument("--ids",   nargs="+", type=int,  help="Item IDs spécifiques")
    parser.add_argument("--range", nargs=2,   type=int,  metavar=("START", "END"),
                        help="Plage d'IDs (défaut: 1 15000)")
    parser.add_argument("--out",     default="../Data/drops.json", help="Fichier de sortie")
    parser.add_argument("--workers", type=int, default=WORKERS,   help="Requêtes parallèles")
    args = parser.parse_args()

    CACHE_DIR.mkdir(exist_ok=True)
    zones = load_zone_map()

    if args.ids:
        item_ids = args.ids
    elif args.range:
        item_ids = list(range(args.range[0], args.range[1] + 1))
    else:
        item_ids = list(range(*DEFAULT_RANGE))

    total = len(item_ids)
    print(f"\nTraitement de {total} items avec {args.workers} workers parallèles...\n")

    # Charger les drops existants pour merger (évite d'écraser en cas de run partiel)
    out_path = Path(args.out)
    drops: dict[str, list] = {}
    if out_path.exists():
        try:
            existing = json.loads(out_path.read_text(encoding="utf-8"))
            drops = existing.get("drops", {})
            if drops:
                print(f"  Merge avec {len(drops)} items existants dans {out_path}\n")
        except Exception:
            pass

    done = 0

    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = {pool.submit(fetch_item, iid): iid for iid in item_ids}
        for future in as_completed(futures):
            item_id, data = future.result()
            done += 1
            if done % 500 == 0:
                print(f"  [{done:>6}/{total}]  {len(drops)} items avec drops")
            if not data:
                continue
            sources = extract_sources(data, zones)
            if sources:
                drops[str(item_id)] = sources

    out_path.parent.mkdir(parents=True, exist_ok=True)

    output = {
        "version": "1.0",
        "_info": (
            "Généré par scripts/fetch_drops.py depuis Garland Tools. "
            "bNpcNameId = garlandMobId % 10^10. "
            "positions[] vide : le plugin scanne la zone entière sans nav fixe."
        ),
        "drops": drops
    }

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)

    print(f"\nTerminé : {len(drops)} items → {out_path}")


if __name__ == "__main__":
    main()
