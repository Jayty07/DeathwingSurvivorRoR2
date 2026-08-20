"""Read back the DWM1 file and render it, so the conversion can be checked offline.

Usage: python3 tools/render.py art/deathwing.dwm [out.png]
"""

import struct
import sys

import numpy as np
from PIL import Image


class R:
    def __init__(self, d):
        self.d = d
        self.p = 0

    def u32(self):
        v = struct.unpack_from('<I', self.d, self.p)[0]
        self.p += 4
        return v

    def i32(self):
        v = struct.unpack_from('<i', self.d, self.p)[0]
        self.p += 4
        return v

    def f32(self):
        v = struct.unpack_from('<f', self.d, self.p)[0]
        self.p += 4
        return v

    def arr(self, fmt, n):
        v = np.frombuffer(self.d, dtype=fmt, count=n, offset=self.p)
        self.p += v.nbytes
        return v

    def s(self):
        n = struct.unpack_from('<H', self.d, self.p)[0]
        self.p += 2
        v = self.d[self.p:self.p + n].decode()
        self.p += n
        return v


def load(path):
    r = R(open(path, 'rb').read())
    assert r.d[:4] == b'DWM1'
    r.p = 8
    bones = []
    for _ in range(r.u32()):
        bones.append({'name': r.s(), 'parent': r.i32(), 'local': r.arr('<f4', 3).copy()})
    textures = []
    for _ in range(r.u32()):
        t = {'name': r.s(), 'format': r.u32(), 'width': r.u32(), 'height': r.u32(),
             'mips': r.u32()}
        n = r.u32()
        t['data'] = r.d[r.p:r.p + n]
        r.p += n
        textures.append(t)
    meshes = []
    for _ in range(r.u32()):
        name = r.s()
        n = r.u32()
        pos = r.arr('<f4', n * 3).reshape(n, 3)
        nrm = r.arr('<f4', n * 3).reshape(n, 3)
        uv = r.arr('<f4', n * 2).reshape(n, 2)
        idx = r.arr('<u2', n * 4).reshape(n, 4)
        wgt = r.arr('<f4', n * 4).reshape(n, 4)
        tris = r.arr('<u2', r.u32()).reshape(-1, 3)
        meshes.append({'name': name, 'pos': pos, 'nrm': nrm, 'uv': uv,
                       'idx': idx, 'wgt': wgt, 'tris': tris})
    anims = []
    for _ in range(r.u32()):
        a = {'name': r.s(), 'duration': r.f32(), 'tracks': {}}
        for _ in range(r.u32()):
            bone = r.u32()
            track = []
            for comps in (3, 4, 3):
                keys = []
                for _ in range(r.u32()):
                    t = r.f32()
                    keys.append((t, r.arr('<f4', comps).copy()))
                track.append(keys)
            a['tracks'][bone] = track
        anims.append(a)
    assert r.p == len(r.d), (r.p, len(r.d))
    return bones, textures, meshes, anims


def trs(t, q, s):
    x, y, z, w = q
    rot = np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])
    m = np.eye(4)
    m[:3, :3] = rot * np.array(s)
    m[:3, 3] = t
    return m


def sample(keys, time, default):
    if not keys:
        return default
    if time <= keys[0][0]:
        return keys[0][1]
    if time >= keys[-1][0]:
        return keys[-1][1]
    for i in range(1, len(keys)):
        if keys[i][0] >= time:
            t0, v0 = keys[i - 1]
            t1, v1 = keys[i]
            u = (time - t0) / (t1 - t0)
            return v0 + (v1 - v0) * u
    return keys[-1][1]


def pose(bones, anim, time):
    world = []
    for i, b in enumerate(bones):
        track = anim['tracks'].get(i) if anim else None
        t = sample(track[0], time, b['local']) if track else b['local']
        q = sample(track[1], time, np.array([0, 0, 0, 1.0])) if track else np.array([0, 0, 0, 1.0])
        s = sample(track[2], time, np.array([1.0, 1, 1])) if track else np.array([1.0, 1, 1])
        local = trs(t, q, s)
        world.append(world[b['parent']] @ local if b['parent'] >= 0 else local)
    return world


def bindposes(bones):
    world = []
    for b in bones:
        m = np.eye(4)
        m[:3, 3] = b['local']
        world.append(world[b['parent']] @ m if b['parent'] >= 0 else m)
    return [np.linalg.inv(m) for m in world]


def skin(mesh, bones, world, binds):
    mats = np.array([world[i] @ binds[i] for i in range(len(bones))])
    v = np.concatenate([mesh['pos'], np.ones((len(mesh['pos']), 1))], axis=1)
    out = np.zeros((len(v), 3))
    for k in range(4):
        wgt = mesh['wgt'][:, k]
        if not wgt.any():
            continue
        m = mats[mesh['idx'][:, k]]
        out += (np.einsum('nij,nj->ni', m, v)[:, :3]) * wgt[:, None]
    return out


def render(verts, tris, size=520, label=''):
    img = np.zeros((size, size, 3), np.float32)
    depth = np.full((size, size), 1e9)
    lo, hi = verts.min(0), verts.max(0)
    center = (lo + hi) / 2
    scale = size * 0.42 / max(hi - lo)
    # Camera: looking down -Z at the model's left side, Y up.
    x = (verts[:, 0] - center[0]) * scale + size / 2
    y = size / 2 - (verts[:, 1] - center[1]) * scale
    z = verts[:, 2]
    p = np.stack([x, y, z], 1)
    for tri in tris:
        a, b, c = p[tri[0]], p[tri[1]], p[tri[2]]
        e1, e2 = b - a, c - a
        area = e1[0] * e2[1] - e1[1] * e2[0]
        if area == 0:
            continue
        n = np.cross(verts[tri[1]] - verts[tri[0]], verts[tri[2]] - verts[tri[0]])
        norm = np.linalg.norm(n)
        shade = 0.25 + 0.75 * abs(n[2] / norm) if norm else 0.5
        minx, maxx = int(max(0, min(a[0], b[0], c[0]))), int(min(size - 1, max(a[0], b[0], c[0])))
        miny, maxy = int(max(0, min(a[1], b[1], c[1]))), int(min(size - 1, max(a[1], b[1], c[1])))
        if minx > maxx or miny > maxy:
            continue
        xs, ys = np.meshgrid(np.arange(minx, maxx + 1), np.arange(miny, maxy + 1))
        w0 = ((b[0] - a[0]) * (ys - a[1]) - (b[1] - a[1]) * (xs - a[0])) / area
        w1 = ((c[0] - b[0]) * (ys - b[1]) - (c[1] - b[1]) * (xs - b[0])) / area
        w2 = 1 - w0 - w1
        mask = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not mask.any():
            continue
        zz = w1 * a[2] + w2 * b[2] + w0 * c[2]
        sub = depth[miny:maxy + 1, minx:maxx + 1]
        hit = mask & (zz < sub)
        sub[hit] = zz[hit]
        colour = np.array([1.0, 0.55, 0.25]) * shade
        img[miny:maxy + 1, minx:maxx + 1][hit] = colour
    out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    return out


if __name__ == '__main__':
    bones, textures, meshes, anims = load(sys.argv[1])
    print('bones', len(bones), 'meshes', [(m['name'], len(m['pos'])) for m in meshes],
          'anims', [a['name'] for a in anims])
    binds = bindposes(bones)
    shots = [('rest', None, 0.0)]
    for name, time in (('Stand', 1.0), ('Attack C', 0.8), ('Spell A', 1.0), ('Spell H', 0.6)):
        anim = next((a for a in anims if a['name'] == name), None)
        if anim:
            shots.append((name, anim, time))
    tiles = []
    for label, anim, time in shots:
        world = pose(bones, anim, time)
        verts = np.concatenate([skin(m, bones, world, binds) for m in meshes])
        tris = []
        offset = 0
        for m in meshes:
            tris.append(m['tris'].astype(np.int64) + offset)
            offset += len(m['pos'])
        img = render(verts, np.concatenate(tris))
        tiles.append(img)
        print('rendered', label)
    sheet = Image.new('RGB', (520 * len(tiles), 520))
    for i, t in enumerate(tiles):
        sheet.paste(t, (520 * i, 0))
    out_path = sys.argv[2] if len(sys.argv) > 2 else 'check.png'
    sheet.save(out_path)
    print('saved', out_path)
