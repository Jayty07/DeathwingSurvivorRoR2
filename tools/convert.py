"""Convert the HoTS Deathwing MDX into the runtime format the mod loads (DWM1).

MDX space is right-handed, Z-up, X-forward, Y-left. Unity is left-handed, Y-up,
Z-forward, X-right, so every vector is remapped (x, y, z) -> (-y, z, x); that swap
is a reflection, hence quaternions flip sign on w and triangle winding is reversed.
Distances stay in MDX units - the mod scales the model root instead.
"""

import math
import struct
import sys

import mdx

# Sequences the mod actually plays, plus a few spares for retuning.
WANTED = [
    'Stand', 'Stand Ready', 'Walk A', 'Walk A Start',
    'Attack A', 'Attack B', 'Attack C',
    'Spell A Start', 'Spell A', 'Spell A End',
    'Spell B', 'Spell C Start', 'Spell C', 'Spell D',
    'Spell H Start', 'Spell H', 'Spell H End',
    'Spell I', 'Spell J',
    'Spell Z Start', 'Spell Z', 'Spell Z End',
    'Death', 'Taunt',
]

# Clips are converted as authored - the rig's own motion is what the animator drew, and editing it
# reads as a stiffer, subtly wrong dragon - with one exception: on the clips below, the root bone's
# travel is held at its first frame. Those are the ones the game itself moves him through (walking,
# flying, diving, channelling in place), and the rig also travels during them: the flight loop lifts
# the root some 350 units and then snaps back when it repeats, and Dragonflight's takeoff carries him
# clean off the map. Holding the root leaves every joint's animation intact and lets the game's own
# movement provide the travel.
ROOT_HELD = {
    'Stand', 'Stand Ready', 'Walk A', 'Walk A Start', 'Spell A', 'Spell B',
    'Spell H Start', 'Spell H', 'Spell H End', 'Spell Z Start', 'Spell Z', 'Spell Z End',
}

POS_TOLERANCE = 0.05
ROT_TOLERANCE = 0.99999
SCALE_TOLERANCE = 0.002


def vec(v):
    return (-v[1], v[2], v[0])


def quat(q):
    x, y, z, w = q
    return (-y, z, x, -w)


def scale(s):
    return (abs(s[1]), abs(s[2]), abs(s[0]))


class Writer:
    def __init__(self):
        self.parts = []

    def u32(self, v):
        self.parts.append(struct.pack('<I', v))

    def i32(self, v):
        self.parts.append(struct.pack('<i', v))

    def f32(self, v):
        self.parts.append(struct.pack('<f', v))

    def floats(self, vs):
        self.parts.append(struct.pack('<%df' % len(vs), *vs))

    def u16s(self, vs):
        self.parts.append(struct.pack('<%dH' % len(vs), *vs))

    def string(self, s):
        b = s.encode('utf-8')
        self.parts.append(struct.pack('<H', len(b)) + b)

    def raw(self, b):
        self.parts.append(b)

    def data(self):
        return b''.join(self.parts)


def dds_payload(path):
    d = open(path, 'rb').read()
    header = struct.unpack_from('<7I', d, 4)
    height, width, mips = header[2], header[3], header[6]
    fourcc = d[84:88]
    fmt = {b'DXT1': 1, b'DXT5': 5}[fourcc]
    return fmt, width, height, max(mips, 1), d[128:]


def sample(track, frame, default):
    """Value of an MDX track at a frame, linearly interpolated."""
    if track is None or not track['keys']:
        return default
    keys = track['keys']
    if frame <= keys[0][0]:
        return keys[0][1]
    if frame >= keys[-1][0]:
        return keys[-1][1]
    for i in range(1, len(keys)):
        if keys[i][0] >= frame:
            f0, v0 = keys[i - 1]
            f1, v1 = keys[i]
            t = (frame - f0) / float(f1 - f0)
            return [a + (b - a) * t for a, b in zip(v0, v1)]
    return keys[-1][1]


def keys_for(track, seq, default, converter):
    """Keys inside a sequence's frame range, in seconds, converted to Unity space.

    MDX stores every sequence on one global timeline, so a track whose keys stop inside this
    sequence must be held, not interpolated on towards the next sequence's keys: doing that
    turns a still pose into a six second drift that snaps back when the clip loops. The track
    is therefore clipped to the keys inside the range and clamped at both ends.
    """
    start, end = seq['start'], seq['end']
    inside = None
    if track is not None:
        inside = {'keys': [(f, v) for f, v in track['keys'] if start <= f <= end]}
        if not inside['keys']:
            # Nothing authored in this range: the bone holds one pose for the whole sequence.
            inside = {'keys': [(start, sample(track, start, default))]}

    frames = set([start, end])
    if inside is not None:
        frames.update(f for f, _ in inside['keys'])
    out = []
    for f in sorted(frames):
        out.append(((f - start) / 1000.0, converter(sample(inside, f, default))))
    return out


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def reduce_keys(keys, tolerance, rotation=False):
    """Drop keys that linear interpolation already reproduces."""
    if len(keys) < 3:
        return keys
    out = [keys[0]]
    for i in range(1, len(keys) - 1):
        t0, v0 = out[-1]
        t1, v1 = keys[i]
        t2, v2 = keys[i + 1]
        span = t2 - t0
        if span <= 0:
            continue
        u = (t1 - t0) / span
        guess = [a + (b - a) * u for a, b in zip(v0, v2)]
        if rotation:
            if abs(dot(guess, v1)) / (math.sqrt(dot(guess, guess)) or 1) < tolerance:
                out.append(keys[i])
        elif max(abs(a - b) for a, b in zip(guess, v1)) > tolerance:
            out.append(keys[i])
    out.append(keys[-1])
    return out


def build_skin(geoset, object_to_bone):
    """Classic MDX vertex groups -> up to four weighted bone indices per vertex."""
    groups = []
    offset = 0
    for count in geoset['matrixGroups']:
        ids = geoset['matrixIndices'][offset:offset + count]
        offset += count
        bones = [object_to_bone[i] for i in ids if i in object_to_bone][:4]
        if not bones:
            bones = [0]
        groups.append(bones)

    indices = []
    weights = []
    for group in geoset['vertexGroups']:
        bones = groups[group] if group < len(groups) else [0]
        weight = 1.0 / len(bones)
        padded = list(bones) + [0] * (4 - len(bones))
        indices.append(padded)
        weights.append([weight] * len(bones) + [0.0] * (4 - len(bones)))
    return indices, weights


def triangles(geoset, normals):
    """Reversed winding, verified against the model's own normals."""
    faces = geoset['faces']
    forward = 0
    for i in range(0, min(len(faces), 3000), 3):
        a, b, c = faces[i], faces[i + 1], faces[i + 2]
        pa, pb, pc = (normals[a], normals[b], normals[c])
        # Geometric normal from the reversed winding (a, c, b).
        va = geoset['unity'][a]
        vb = geoset['unity'][c]
        vc = geoset['unity'][b]
        e1 = [vb[j] - va[j] for j in range(3)]
        e2 = [vc[j] - va[j] for j in range(3)]
        cross = [e1[1] * e2[2] - e1[2] * e2[1],
                 e1[2] * e2[0] - e1[0] * e2[2],
                 e1[0] * e2[1] - e1[1] * e2[0]]
        avg = [(pa[j] + pb[j] + pc[j]) / 3.0 for j in range(3)]
        forward += 1 if dot(cross, avg) > 0 else -1
    reverse = forward > 0
    out = []
    for i in range(0, len(faces), 3):
        a, b, c = faces[i], faces[i + 1], faces[i + 2]
        out.extend((a, c, b) if reverse else (a, b, c))
    return out, reverse


def main(mdx_path, diffuse_path, emissive_path, out_path):
    m = mdx.parse(mdx_path)
    pivots = m['pivots']
    bones = m['bones']
    object_to_bone = {b['objectId']: i for i, b in enumerate(bones)}

    w = Writer()
    w.raw(b'DWM1')
    w.u32(1)

    # Skeleton: rest pose is every bone sitting on its pivot with no rotation.
    w.u32(len(bones))
    rest = []
    for i, b in enumerate(bones):
        pivot = vec(pivots[b['objectId']])
        parent = object_to_bone.get(b['parentId'], -1)
        if parent >= 0:
            ppivot = vec(pivots[bones[parent]['objectId']])
            local = [pivot[j] - ppivot[j] for j in range(3)]
        else:
            local = list(pivot)
        rest.append(local)
        w.string(b['name'])
        w.i32(parent)
        w.floats(local)

    # Textures ship as their original DXT blocks, mip chain included.
    w.u32(2)
    for name, path in (('diffuse', diffuse_path), ('emissive', emissive_path)):
        fmt, width, height, mips, payload = dds_payload(path)
        w.string(name)
        w.u32(fmt)
        w.u32(width)
        w.u32(height)
        w.u32(mips)
        w.u32(len(payload))
        w.raw(payload)
        print('texture %s %dx%d dxt%d mips=%d %.1fMB'
              % (name, width, height, fmt, mips, len(payload) / 1e6))

    w.u32(len(m['geosets']))
    for index, g in enumerate(m['geosets']):
        g['unity'] = [vec(v) for v in g['vertices']]
        normals = [vec(n) for n in g['normals']]
        tris, reversed_winding = triangles(g, normals)
        indices, weights = build_skin(g, object_to_bone)
        uvs = g['uvs'][0]

        w.string('geoset%d' % index)
        w.u32(len(g['unity']))
        for v in g['unity']:
            w.floats(v)
        for n in normals:
            w.floats(n)
        for uv in uvs:
            # V is written as authored, not flipped for Unity's bottom-up texture space: the DXT
            # blocks are handed to the GPU exactly as the DDS stores them (top row first), so the
            # texture is already mirrored on load and the two conventions cancel out.
            w.floats((uv[0], uv[1]))
        for bone_ids in indices:
            w.u16s(bone_ids)
        for weight in weights:
            w.floats(weight)
        w.u32(len(tris))
        w.u16s(tris)
        print('geoset%d verts=%d tris=%d reversedWinding=%s'
              % (index, len(g['unity']), len(tris) // 3, reversed_winding))

    sequences = {s['name']: s for s in m['sequences']}
    wanted = [name for name in WANTED if name in sequences]
    w.u32(len(wanted))
    total_keys = 0
    for name in wanted:
        seq = sequences[name]
        w.string(name)
        w.f32((seq['end'] - seq['start']) / 1000.0)
        tracks = []
        for i, b in enumerate(bones):
            pos = keys_for(b['tracks'].get('KGTR'), seq, [0.0, 0.0, 0.0], vec)
            pos = [(t, [rest[i][j] + v[j] for j in range(3)]) for t, v in pos]
            rot = keys_for(b['tracks'].get('KGRT'), seq, [0.0, 0.0, 0.0, 1.0], quat)
            scl = keys_for(b['tracks'].get('KGSC'), seq, [1.0, 1.0, 1.0], scale)
            if name in ROOT_HELD and object_to_bone.get(b['parentId'], -1) < 0:
                pos = pos[:1]
            pos = reduce_keys(pos, POS_TOLERANCE)
            rot = reduce_keys(rot, ROT_TOLERANCE, rotation=True)
            scl = reduce_keys(scl, SCALE_TOLERANCE)
            if len(pos) <= 2 and all(max(abs(v[j] - rest[i][j]) for j in range(3)) < POS_TOLERANCE
                                     for _, v in pos):
                pos = []
            if len(rot) <= 2 and all(abs(abs(v[3]) - 1.0) < 1e-4 for _, v in rot):
                rot = []
            if len(scl) <= 2 and all(max(abs(c - 1.0) for c in v) < SCALE_TOLERANCE for _, v in scl):
                scl = []
            if pos or rot or scl:
                tracks.append((i, pos, rot, scl))
        w.u32(len(tracks))
        for i, pos, rot, scl in tracks:
            w.u32(i)
            for keys, comps in ((pos, 3), (rot, 4), (scl, 3)):
                w.u32(len(keys))
                for t, v in keys:
                    w.f32(t)
                    w.floats(v[:comps])
            total_keys += len(pos) + len(rot) + len(scl)
        print('anim %-16s %5.2fs tracks=%d' % (name, (seq['end'] - seq['start']) / 1000.0, len(tracks)))

    data = w.data()
    open(out_path, 'wb').write(data)
    print('wrote %s: %.2f MB, %d keys, %d bones, %d anims'
          % (out_path, len(data) / 1e6, total_keys, len(bones), len(wanted)))


if __name__ == '__main__':
    main(*sys.argv[1:5])
