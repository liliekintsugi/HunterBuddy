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
from pathlib import Path

GARLAND_URL   = "https://www.garlandtools.org/db/doc/item/en/3/{}.json"
TERRITORY_URL = "https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/TerritoryType.csv"
PLACE_URL     = "https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/PlaceName.csv"

BNPC_MOD  = 10_000_000_000   # garlandId % BNPC_MOD = bNpcNameId
CACHE_DIR = Path("cache")
DEFAULT_RANGE = (1, 15_000)
REQUEST_DELAY = 0.15          # secondes entre deux requêtes réseau


# ─── Data helpers ─────────────────────────────────────────────────────────────

def load_csv_index(url: str, key_col: int, val_col: int) -> dict[int, str]:
    """Télécharge un CSV ffxiv-datamining et retourne {rowId: valeur}."""
    print(f"Téléchargement {url.split('/')[-1]}...")
    r = requests.get(url, timeout=30)
    r.raise_for_status()
    lines = r.text.splitlines()
    # Ligne 0 : numéros de colonnes  |  Ligne 1 : noms  |  Ligne 2 : types  |  Ligne 3+ : données
    result: dict[int, str] = {}
    for line in lines[3:]:
        parts = line.split(",")
        try:
            row_id = int(parts[key_col])
            value  = parts[val_col].strip().strip('"')
            if row_id > 0 and value:
                result[row_id] = value
        except (ValueError, IndexError):
            continue
    print(f"  {len(result)} entrées chargées.")
    return result


def load_territory_names() -> dict[int, str]:
    """
    TerritoryType.csv : colonne 0 = RowId, colonne 6 = PlaceName (ref row ID).
    PlaceName.csv     : colonne 0 = RowId, colonne 1 = Name.
    On résout la référence pour avoir des noms lisibles.
    """
    place_names = load_csv_index(PLACE_URL, 0, 1)

    print(f"Téléchargement TerritoryType.csv...")
    r = requests.get(TERRITORY_URL, timeout=30)
    r.raise_for_status()
    lines = r.text.splitlines()

    territories: dict[int, str] = {}
    for line in lines[3:]:
        parts = line.split(",")
        try:
            territory_id   = int(parts[0])
            place_name_ref = int(parts[6])         # référence vers PlaceName
            name = place_names.get(place_name_ref, "")
            if territory_id > 0 and name:
                territories[territory_id] = name
        except (ValueError, IndexError):
            continue

    print(f"  {len(territories)} territoires résolus.")
    return territories


# ─── Garland Tools ────────────────────────────────────────────────────────────

def fetch_item(item_id: int) -> dict | None:
    cache_path = CACHE_DIR / f"{item_id}.json"

    if cache_path.exists():
        raw = cache_path.read_text(encoding="utf-8")
        return json.loads(raw) if raw != "{}" else None

    try:
        r = requests.get(GARLAND_URL.format(item_id), timeout=15)
        if r.status_code == 404:
            cache_path.write_text("{}")
            return None
        r.raise_for_status()
        data = r.json()
        cache_path.write_text(json.dumps(data, ensure_ascii=False))
        time.sleep(REQUEST_DELAY)
        return data
    except Exception as e:
        print(f"  [!] Item {item_id} : {e}")
        return None


def extract_sources(data: dict, territories: dict[int, str]) -> list[dict]:
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
        territory_id = int(mob.get("z", 0))
        key = (bnpc_name_id, territory_id)

        if key in seen or bnpc_name_id == 0 or territory_id == 0:
            continue
        seen.add(key)

        sources.append({
            "bNpcNameId":  bnpc_name_id,
            "mobName":     mob.get("n", "Unknown"),
            "territoryId": territory_id,
            "zoneName":    territories.get(territory_id, f"Zone {territory_id}"),
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
    parser.add_argument("--out",   default="../Data/drops.json", help="Fichier de sortie")
    args = parser.parse_args()

    CACHE_DIR.mkdir(exist_ok=True)
    territories = load_territory_names()

    if args.ids:
        item_ids = args.ids
    elif args.range:
        item_ids = list(range(args.range[0], args.range[1] + 1))
    else:
        item_ids = list(range(*DEFAULT_RANGE))

    print(f"\nTraitement de {len(item_ids)} items...\n")

    drops : dict[str, list] = {}
    total = len(item_ids)

    for i, item_id in enumerate(item_ids):
        if i % 500 == 0:
            print(f"  [{i:>6}/{total}]  {len(drops)} items avec drops trouvés")

        data = fetch_item(item_id)
        if not data:
            continue

        sources = extract_sources(data, territories)
        if sources:
            drops[str(item_id)] = sources

    out_path = Path(args.out)
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
