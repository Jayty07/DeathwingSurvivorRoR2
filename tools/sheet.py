"""Render a contact sheet of clips from a DWM1 payload, to see what each one depicts.

Usage: python3 tools/sheet.py art/deathwing.dwm out.png "Spell Z" "Spell H" ...
"""

import sys

import numpy as np
from PIL import Image, ImageDraw

import render


def frame(bones, binds, meshes, anim, time, box):
    world = render.pose(bones, anim, time)
    verts = np.concatenate([render.skin(m, bones, world, binds) for m in meshes])
    tris = []
    offset = 0
    for m in meshes:
        tris.append(m['tris'].astype(np.int64) + offset)
        offset += len(m['pos'])
    # The framing box rides along as unreferenced vertices so every tile shares one camera and
    # the motion of the clip is visible rather than being fitted away.
    return render.render(np.concatenate([verts, box]), np.concatenate(tris))


def main(path, out, names):
    bones, textures, meshes, anims = render.load(path)
    binds = render.bindposes(bones)
    rest = render.pose(bones, None, 0.0)
    verts = np.concatenate([render.skin(m, bones, rest, binds) for m in meshes])
    lo, hi = verts.min(0), verts.max(0)
    span = (hi - lo).max() * 1.1
    center = (lo + hi) / 2
    box = np.array([center - span / 2, center + span / 2])

    columns = 5
    rows = []
    for name in names:
        anim = next((a for a in anims if a['name'] == name), None)
        if not anim:
            print('missing', name)
            continue
        tiles = [frame(bones, binds, meshes, anim, anim['duration'] * i / (columns - 1.0), box)
                 for i in range(columns)]
        strip = Image.new('RGB', (tiles[0].width * columns, tiles[0].height))
        for i, tile in enumerate(tiles):
            strip.paste(tile, (tile.width * i, 0))
        ImageDraw.Draw(strip).text((8, 8), '%s  %.2fs' % (name, anim['duration']), fill=(255, 255, 255))
        rows.append(strip)

    sheet = Image.new('RGB', (rows[0].width, sum(r.height for r in rows)))
    y = 0
    for row in rows:
        sheet.paste(row, (0, y))
        y += row.height
    sheet.save(out)
    print('wrote', out, sheet.size)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3:])
