"""
SplitSpriteSheet.py - Reusable utility to split pixel-art pet sprite sheets into individual animation PNG strips.
"""
import sys
import os
from PIL import Image

ROW_DEFS = [
    {"name": "Idle",  "startY": 13,  "endY": 57,  "minFrameX": 190},
    {"name": "Walk",  "startY": 63,  "endY": 107, "minFrameX": 190},
    {"name": "Run",   "startY": 115, "endY": 162, "minFrameX": 190},
    {"name": "Eat",   "startY": 168, "endY": 211, "minFrameX": 190},
    {"name": "Bath",  "startY": 220, "endY": 263, "minFrameX": 190},
    {"name": "Sleep", "startY": 270, "endY": 310, "minFrameX": 190},
    {"name": "Happy", "startY": 311, "endY": 359, "minFrameX": 190},
    {"name": "Hurt",  "startY": 363, "endY": 403, "minFrameX": 190}
]

def split_sprite_sheet(source_path, output_dir="Assets/Pets/Cat", frame_size=64, target_baseline=56):
    if not os.path.exists(source_path):
        print(f"Error: Source file {source_path} not found.")
        sys.exit(1)

    os.makedirs(output_dir, exist_ok=True)
    img = Image.open(source_path).convert("RGBA")
    w, h = img.size
    pixels = img.load()

    print(f"=== AUTOMATIC SPRITE SHEET EXTRACTION START ===")
    print(f"Source: {source_path} ({w}x{h})")
    print(f"Destination: {output_dir}")

    for row in ROW_DEFS:
        name = row["name"]
        startY, endY = row["startY"], row["endY"]
        minFrameX = row["minFrameX"]

        # 1. Detect column occupancy for frames
        col_has_pixels = [False] * w
        for x in range(minFrameX, w):
            for y in range(startY, endY + 1):
                if pixels[x, y][3] > 10:
                    col_has_pixels[x] = True
                    break

        # 2. Cluster frames horizontally
        clusters = []
        in_cluster = False
        start_x = 0
        for x in range(minFrameX, w):
            if col_has_pixels[x] and not in_cluster:
                in_cluster = True
                start_x = x
            elif not col_has_pixels[x] and in_cluster:
                in_cluster = False
                clusters.append((start_x, x - 1))
        if in_cluster:
            clusters.append((start_x, w - 1))

        if not clusters:
            print(f"Warning: No frames detected for {name}!")
            continue

        # 3. Find exact bounding boxes and row baseline
        frame_boxes = []
        row_baseline = 0
        for start_c, end_c in clusters:
            min_x, max_x = 9999, -1
            min_y, max_y = 9999, -1
            for x in range(start_c, end_c + 1):
                for y in range(startY, endY + 1):
                    if pixels[x, y][3] > 10:
                        if x < min_x: min_x = x
                        if x > max_x: max_x = x
                        if y < min_y: min_y = y
                        if y > max_y: max_y = y
            if max_y > row_baseline:
                row_baseline = max_y
            frame_boxes.append({
                "minX": min_x, "maxX": max_x, "width": max_x - min_x + 1,
                "minY": min_y, "maxY": max_y, "height": max_y - min_y + 1
            })

        frame_count = len(frame_boxes)
        strip_w = frame_count * frame_size
        strip_h = frame_size
        strip_img = Image.new("RGBA", (strip_w, strip_h), (0, 0, 0, 0))
        strip_pixels = strip_img.load()

        # 4. Copy each frame into the strip with baseline alignment
        for i, fb in enumerate(frame_boxes):
            cell_start_x = i * frame_size
            offset_x = (frame_size - fb["width"]) // 2

            bottom_diff = row_baseline - fb["maxY"]
            dst_bottom = target_baseline - bottom_diff
            dst_y = dst_bottom - fb["height"] + 1

            for sy in range(fb["minY"], fb["maxY"] + 1):
                dy = dst_y + (sy - fb["minY"])
                if dy < 0 or dy >= frame_size:
                    continue
                for sx in range(fb["minX"], fb["maxX"] + 1):
                    dx = cell_start_x + offset_x + (sx - fb["minX"])
                    if dx < cell_start_x or dx >= cell_start_x + frame_size:
                        continue
                    p = pixels[sx, sy]
                    if p[3] > 0:
                        strip_pixels[dx, dy] = p

        out_file = os.path.join(output_dir, f"{name}.png")
        strip_img.save(out_file, "PNG")
        print(f"[OK] Extracted {name}.png -> Frames: {frame_count}, Size: {strip_w}x{strip_h}")

    print("=== EXTRACTION COMPLETE ===")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python SplitSpriteSheet.py <source_image_path> [output_dir]")
        sys.exit(1)
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else "Assets/Pets/Cat"
    split_sprite_sheet(src, out)
