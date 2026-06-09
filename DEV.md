# HunterBuddy — Notes de développement

## Stack technique

| Composant | Détail |
|---|---|
| Framework | `Dalamud.NET.Sdk/15.0.0` (Dalamud v15, .NET 10) |
| ImGui | `using Dalamud.Bindings.ImGui` (plus `ImGuiNET` depuis v15) |
| Excel/Lumina | `Lumina.Excel.Sheets` (plus `GeneratedSheets` depuis Lumina 3.x) |
| ClientStructs | `FFXIVClientStructs.FFXIV.Client.*` |

---

## Pièges API Dalamud v15

### ImGui
```csharp
// ❌ Ancien (avant v15)
using ImGuiNET;
// ✅ Correct
using Dalamud.Bindings.ImGui;
```

### Lumina — noms d'items
```csharp
// ❌ .ToString() ne marche pas en FR/JP
i.Name.ToString()
// ✅ Extrait le texte brut dans la langue du client
i.Name.ExtractText()
```

### Lumina — accès aux rows
`GetRow()` retourne un **struct value-type**, pas une référence nullable :
```csharp
// ❌ double ?. invalide sur un struct
sheet?.GetRow(id)?.Abbreviation.ExtractText()
// ✅ un seul ?. sur sheet, puis accès direct
sheet?.GetRow(id).Abbreviation.ExtractText()
```

### IClientState — LocalPlayer retiré
```csharp
// ❌ n'existe plus dans IClientState depuis v15
Plugin.ClientState.LocalPlayer
// ✅ déplacé sur IObjectTable
Plugin.ObjectTable.LocalPlayer
```

### IClientState — territoire courant
```csharp
Plugin.ClientState.TerritoryType  // uint, inchangé
```

---

## IPC

### vnavmesh
```csharp
ICallGateSubscriber<Vector3, bool, bool>  "vnavmesh.SimpleMove.PathfindAndMoveTo"
ICallGateSubscriber<bool>                 "vnavmesh.Nav.IsReady"
ICallGateSubscriber<bool>                 "vnavmesh.SimpleMove.PathfindInProgress"
ICallGateSubscriber<object>               "vnavmesh.Path.Stop"
```
- Navigue **dans la zone courante uniquement** — pas de téléport inter-zones.

### RotationSolverReborn
```csharp
ICallGateSubscriber<byte, object>  "RotationSolverReborn.ChangeOperatingMode"
// StateCommandType : 0=Off, 1=Auto, 2=TargetOnly, 3=Manual
```

---

## Changement de classe (gear sets)
```csharp
// Lecture des gear sets
var gsm   = RaptureGearsetModule.Instance();
var entry = gsm->GetGearset(index);   // 0-indexed
entry->ClassJob   // byte → job ID
entry->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)

// Équipement via commande (1-indexed en jeu)
Plugin.CommandManager.ProcessCommand($"/gearset change {index + 1}");

// Vérifier que le switch est effectif
Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId  // uint
```

---

## drops.json — format et génération

- Généré par `scripts/fetch_drops.py` depuis Garland Tools
- `bNpcNameId = garlandMobId % 10_000_000_000`
- `territoryId` = Map.csv col 8 (TerritoryType ID Dalamud)
- Zones HW+ hors Map.csv → `territoryId: 0`, `zoneName: "Garland zone X"` (farm manuel)
- `positions: []` → le plugin scanne la zone entière sans nav fixe

```json
{
  "drops": {
    "5310": [{
      "bNpcNameId": 2659,
      "mobName": "Eoraptor",
      "territoryId": 1067,
      "zoneName": "...",
      "positions": [],
      "dropRate": 1.0
    }]
  }
}
```

Pour régénérer / étendre :
```bash
cd scripts
python fetch_drops.py --range 1 45000 --workers 20 --out ../Data/drops.json
```

---

## Machine à états (HuntService)

```
Idle
 └─ Start()
     └─ SelectingTarget
         ├─ SwitchingJob      (si gear set configuré et job ≠ cible)
         ├─ WaitingForZone    (si TerritoryType ≠ source.TerritoryId)
         ├─ NavigatingToSpawn (si positions[] non vide)
         └─ SearchingMob
             └─ Engaging
                 └─ InCombat
                     └─ Looting
                         └─ CheckingInventory
                             └─ SelectingTarget  (boucle) ou AllDone
```

**Règle importante** : `CheckingInventory` repasse **toujours** par `SelectingTarget` (jamais directement vers `NavigatingToSpawn`) pour éviter un crash sur `_currentSpawn == null`.

---

## CI / Release

- Tag `vX.Y.Z` → build Windows + zip `bin/Release/*` → GitHub Release + mise à jour `repo.json`
- `repo.json` : tableau JSON `[{...}]` — le script PowerShell doit entourer avec `"[$(...)]"` sinon `ConvertTo-Json` perd le tableau sur 1 élément
- `DalamudApiLevel: 16` (Dalamud v15 = API level 16)
- URL custom repo Dalamud : `https://raw.githubusercontent.com/liliekintsugi/HunterBuddy/main/repo.json`
