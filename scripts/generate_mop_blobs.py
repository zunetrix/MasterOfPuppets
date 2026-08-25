import os
import sys
import json
import gzip
import base64
import math
import argparse

DEFAULT_CONFIG_PATH = os.path.expandvars(r"%APPDATA%\XIVLauncher\pluginConfigs\MasterOfPuppets.json")

def compress_string(input_str: str) -> str:
    utf8_bytes = input_str.encode("utf-8")
    compressed = gzip.compress(utf8_bytes, compresslevel=9)
    return base64.b64encode(compressed).decode("ascii")

def decompress_string(b64_str: str) -> str:
    compressed = base64.b64decode(b64_str.strip())
    return gzip.decompress(compressed).decode("utf-8")

def export_formation_blob(formation_dict) -> str:
    json_str = json.dumps(formation_dict, separators=(",", ":"))
    return "MOPF1:" + compress_string(json_str)

def export_macro_blob(macro_dict) -> str:
    json_str = json.dumps(macro_dict, separators=(",", ":"))
    return compress_string(json_str)

def get_32_ordered_cids(config_path: str):
    if os.path.exists(config_path):
        try:
            with open(config_path, "r", encoding="utf-8") as f:
                cfg = json.load(f)
            for group in cfg.get("CidsGroups", []):
                if group.get("Name") == "32 Ordered":
                    cids = group.get("Cids", [])
                    if len(cids) >= 32:
                        return cids[:32]
            char_cids = [c["Cid"] for c in cfg.get("Characters", []) if "Cid" in c]
            if len(char_cids) >= 32:
                return char_cids[:32]
        except Exception as e:
            print(f"Warning: Failed to read CIDs from config ({e}), using mock CIDs.")
    # Fallback to generic mock CIDs
    return [1000000000000000 + i for i in range(1, 33)]

def normalize_degrees(deg: float) -> float:
    while deg > 180.0:
        deg -= 360.0
    while deg <= -180.0:
        deg += 360.0
    return deg

def generate_circle_ring(count: int, radius: float, is_ccw: bool, assigned_cids):
    points = []
    for i in range(count):
        a = (2.0 * math.pi * i) / count - math.pi / 2.0
        x = radius * math.cos(a)
        z = radius * math.sin(a)
        
        tangent_deg = normalize_degrees(math.atan2(-math.sin(a), math.cos(a)) * (180.0 / math.pi))
        if is_ccw:
            face_angle = normalize_degrees(tangent_deg + 180.0)
        else:
            face_angle = tangent_deg
            
        cid_list = [assigned_cids[i]] if i < len(assigned_cids) else []
        points.append({
            "Offset": {"X": round(x, 4), "Y": 0.0, "Z": round(z, 4)},
            "Angle": round(face_angle, 2),
            "Cids": cid_list,
            "GroupIds": []
        })
    return points

def build_moon_orbit(config_path: str = DEFAULT_CONFIG_PATH,
                     out_dirs: list = None,
                     leader: str = "Leader Character@World",
                     phase: str = ".12",
                     mode: str = "continuous"):
    if out_dirs is None:
        out_dirs = ["."]

    cids = get_32_ordered_cids(config_path)

    # 1. Formation
    formation_points = [{
        "Offset": {"X": 0.0, "Y": 0.0, "Z": 0.0},
        "Angle": 0.0,
        "Cids": [],
        "GroupIds": []
    }]

    ring1 = generate_circle_ring(8, 0.6, is_ccw=False, assigned_cids=cids[0:8])
    ring2 = generate_circle_ring(8, 1.0, is_ccw=True,  assigned_cids=cids[8:16])
    ring3 = generate_circle_ring(8, 1.4, is_ccw=False, assigned_cids=cids[16:24])
    ring4 = generate_circle_ring(8, 2.0, is_ccw=True,  assigned_cids=cids[24:32])

    formation_points.extend(ring1)
    formation_points.extend(ring2)
    formation_points.extend(ring3)
    formation_points.extend(ring4)

    formation = {
        "Name": "Moon Orbit",
        "Points": formation_points
    }

    # 2. Macro
    commands = []
    jump_beats = [1, 3, 5, 7]

    for char_idx in range(32):
        cid = cids[char_idx]
        ring_idx = char_idx // 8
        slot_in_ring = char_idx % 8
        ring_base_pt = 2 + (ring_idx * 8)
        is_reverse = (ring_idx % 2 == 1)
        jump_beat = jump_beats[ring_idx]

        lines = [
            "/mopif \"$mop_origin_target\" != \"\" /moptarget \"$mop_origin_target\"",
            "/mopif \"$mop_origin_target\" == \"\" /moptarget \"$leader\"",
            "/moploopstart",
            "/mopif \"$mop_origin_target\" != \"\" && me == \"$mop_origin_target\"",
            "    /mopphasewait $phase",
            "/mopelseif \"$mop_origin_target\" == \"\" && me == \"$mop_origin\"",
            "    /mopphasewait $phase",
            "/mopelse"
        ]

        for step in range(8):
            if is_reverse:
                pt_offset = (8 + slot_in_ring - step) % 8
            else:
                pt_offset = (slot_in_ring + step) % 8
            pt_num = ring_base_pt + pt_offset

            lines.append(f"    /mopif \"$anchor\" != \"\" /mopformationgoto \"$formation\" {pt_num} anchor=\"$anchor\" fallback=\"$leader\" $mode")
            lines.append(f"    /mopif \"$anchor\" == \"\" /mopformationgoto \"$formation\" {pt_num} anchor=\"$mop_origin\" fallback=\"$leader\" $mode")

            if step == 0 and slot_in_ring == 0:
                lines.append("    /ac \"Peloton\"")

            if step == 4:
                lines.append("    /gaction \"sprint\"")

            if step == jump_beat:
                lines.append("    /mopif \"$jump\" == \"yes\" /gaction \"jump\"")

            lines.append("    /mopphasewait $phase")

        lines.append("/mopendif")
        lines.append("/moploopend")

        commands.append({
            "Cids": [cid],
            "GroupIds": [],
            "Actions": "\n".join(lines)
        })

    macro = {
        "Name": "Moon Orbit",
        "Variables": f"$phase = {phase}\n$formation = Moon Orbit\n$anchor = $mop_origin_target\n$leader = {leader}\n$mode = {mode}\n$jump = yes",
        "Tags": ["Formations", "Moon Orbit", "Test"],
        "Commands": commands,
        "Color": {"X": 0.35, "Y": 0.75, "Z": 0.95, "W": 1.0},
        "IconId": 60002
    }

    form_blob = export_formation_blob(formation)
    macro_blob = export_macro_blob(macro)

    for dest_dir in out_dirs:
        os.makedirs(dest_dir, exist_ok=True)
        with open(os.path.join(dest_dir, "moon_orbit_formation_blob.txt"), "w", encoding="utf-8") as f:
            f.write(form_blob)
        with open(os.path.join(dest_dir, "moon_orbit_macro_blob.txt"), "w", encoding="utf-8") as f:
            f.write(macro_blob)
        with open(os.path.join(dest_dir, "moon_orbit_macro.json"), "w", encoding="utf-8") as f:
            json.dump(macro, f, indent=2)
        with open(os.path.join(dest_dir, "moon_orbit_macro_export.json"), "w", encoding="utf-8") as f:
            json.dump([macro], f, indent=2)

    print(f"SUCCESS! Compact Macro Blob ({len(macro_blob)} chars):")
    print(macro_blob)

def main():
    parser = argparse.ArgumentParser(description="Generate MasterOfPuppets formation and macro blobs.")
    parser.add_argument("--config", default=DEFAULT_CONFIG_PATH, help="Path to MasterOfPuppets.json config")
    parser.add_argument("--out", nargs="+", default=["."], help="Output directory or directories")
    parser.add_argument("--leader", default="Leader Character@World", help="Leader character name")
    parser.add_argument("--phase", default=".12", help="Phase wait interval in seconds")
    parser.add_argument("--mode", default="continuous", help="Movement mode (natural, precise, continuous)")
    args = parser.parse_args()

    build_moon_orbit(config_path=args.config, out_dirs=args.out, leader=args.leader, phase=args.phase, mode=args.mode)

if __name__ == "__main__":
    main()