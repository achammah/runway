#!/usr/bin/env python3
"""Find our characters inside a scene the model composed.

WHY: a composed scene is one flat image, so it cannot breathe the way the pre-built
rooms do. But our characters have an unmistakable signature — a solid ink-black mass
containing two blank WHITE oval eyes and nothing else. If they can be located in the
finished image, each one can be cut out and animated in-engine (breathe, blink, sway)
over the frozen room, which is where almost all the perceived life of a scene comes from.

The method is the inverse of the emptiness check LANE-BATCH used to catch creatures that
sneaked into rooms that had to be empty: find WHITE blobs, keep the ones fully enclosed
by ink, and group them by the ink mass that encloses them. Two eyes in one mass is a
character. A whiteboard has no ink around it; a dark TV has no white holes inside it.
"""
import sys
from PIL import Image

def find(path, scale=4, ink_max=70, eye_min=210):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    sm = im.resize((W // scale, H // scale))
    px = sm.load(); w, h = sm.size
    ink = [[(px[x, y][0] < ink_max and px[x, y][1] < ink_max and px[x, y][2] < ink_max)
            for y in range(h)] for x in range(w)]
    white = [[(px[x, y][0] > eye_min and px[x, y][1] > eye_min and px[x, y][2] > eye_min)
              for y in range(h)] for x in range(w)]
    seen = [[False] * h for _ in range(w)]
    eyes = []
    for y in range(h):
        for x in range(w):
            if not white[x][y] or seen[x][y]:
                continue
            st = [(x, y)]; seen[x][y] = True; cells = []
            touches_edge = False
            while st:
                cx, cy = st.pop(); cells.append((cx, cy))
                if cx in (0, w - 1) or cy in (0, h - 1):
                    touches_edge = True
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and white[nx][ny] and not seen[nx][ny]:
                        seen[nx][ny] = True; st.append((nx, ny))
            # an eye is a SMALL white blob, not a whiteboard, and never touches the frame
            if touches_edge or not (3 <= len(cells) <= 400):
                continue
            xs = [c[0] for c in cells]; ys = [c[1] for c in cells]
            x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
            # enclosed? walk out from the blob's edge in four directions and require ink
            enclosed = 0
            for (sx, sy, dx, dy) in ((x0, (y0 + y1) // 2, -1, 0), (x1, (y0 + y1) // 2, 1, 0),
                                     ((x0 + x1) // 2, y0, 0, -1), ((x0 + x1) // 2, y1, 0, 1)):
                # Walk outward and look for ink within a few cells. Do NOT abort on the
                # first non-white pixel: downscaling blurs the white/ink boundary into a
                # band of greys, and aborting there found only 1 eye out of 6.
                for step in range(1, 8):
                    nx, ny = sx + dx * step, sy + dy * step
                    if not (0 <= nx < w and 0 <= ny < h):
                        break
                    if ink[nx][ny]:
                        enclosed += 1; break
            if enclosed >= 3:
                eyes.append(((x0 + x1) / 2.0, (y0 + y1) / 2.0, len(cells)))
    # two eyes close together and similar in size = one face
    chars = []
    used = [False] * len(eyes)
    for i, (ex, ey, ea) in enumerate(eyes):
        if used[i]:
            continue
        best = None
        for j in range(i + 1, len(eyes)):
            if used[j]:
                continue
            ox, oy, oa = eyes[j]
            d = ((ex - ox) ** 2 + (ey - oy) ** 2) ** 0.5
            if d < 22 and abs(ey - oy) < 7 and 0.35 < ea / max(oa, 1) < 2.8:
                if best is None or d < best[1]:
                    best = (j, d)
        if best:
            used[i] = used[best[0]] = True
            ox, oy, _ = eyes[best[0]]
            chars.append((((ex + ox) / 2) * scale, ((ey + oy) / 2) * scale))
    return W, H, len(eyes), sorted(chars)

if __name__ == "__main__":
    for p in sys.argv[1:]:
        W, H, ne, chars = find(p)
        print(f"{p}  ({W}x{H})  eye-blobs={ne}  CHARACTERS={len(chars)}")
        for i, (x, y) in enumerate(chars, 1):
            print(f"   #{i} face centre ~({int(x)},{int(y)})")
