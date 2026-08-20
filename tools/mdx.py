"""Minimal MDX (Warcraft 3 / Reforged v800) reader for the HoTS Deathwing port."""

import struct


class Reader:
    def __init__(self, data, pos=0, end=None):
        self.d = data
        self.p = pos
        self.end = len(data) if end is None else end

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

    def u16(self):
        v = struct.unpack_from('<H', self.d, self.p)[0]
        self.p += 2
        return v

    def u8(self):
        v = self.d[self.p]
        self.p += 1
        return v

    def floats(self, n):
        v = struct.unpack_from('<%df' % n, self.d, self.p)
        self.p += 4 * n
        return list(v)

    def tag(self):
        v = self.d[self.p:self.p + 4]
        self.p += 4
        return v

    def string(self, n):
        v = self.d[self.p:self.p + n].split(b'\0')[0].decode('utf-8', 'replace')
        self.p += n
        return v

    def more(self):
        return self.p < self.end


INTERP = {0: 'none', 1: 'linear', 2: 'hermite', 3: 'bezier'}


def read_track(r, comps):
    """KGTR/KGRT/KGSC style keyframe track."""
    count = r.u32()
    interp = INTERP[r.u32()]
    global_seq = r.i32()
    keys = []
    for _ in range(count):
        frame = r.i32()
        value = r.floats(comps)
        if interp in ('hermite', 'bezier'):
            r.floats(comps)  # inTan
            r.floats(comps)  # outTan
        keys.append((frame, value))
    return {'interp': interp, 'globalSeq': global_seq, 'keys': keys}


TRACK_COMPS = {b'KGTR': 3, b'KGRT': 4, b'KGSC': 3}


def read_node(r):
    start = r.p
    size = r.u32()
    node = {
        'name': r.string(80),
        'objectId': r.i32(),
        'parentId': r.i32(),
        'flags': r.u32(),
        'tracks': {},
    }
    while r.p < start + size:
        tag = r.tag()
        if tag in TRACK_COMPS:
            node['tracks'][tag.decode()] = read_track(r, TRACK_COMPS[tag])
        else:
            raise ValueError('unexpected node tag %r at %d' % (tag, r.p - 4))
    r.p = start + size
    return node


def parse(path):
    data = open(path, 'rb').read()
    out = {'sequences': [], 'geosets': [], 'bones': [], 'helpers': [], 'attachments': [],
           'textures': [], 'materials': [], 'pivots': [], 'version': None, 'name': None,
           'geosetAnims': []}
    r = Reader(data, 4)
    while r.more():
        tag = r.tag()
        size = r.u32()
        end = r.p + size
        c = Reader(data, r.p, end)
        if tag == b'VERS':
            out['version'] = c.u32()
        elif tag == b'MODL':
            out['name'] = c.string(80)
            c.string(260)
            out['bounds'] = (c.f32(), c.floats(3), c.floats(3))
            out['blendTime'] = c.u32()
        elif tag == b'SEQS':
            while c.more():
                seq = {
                    'name': c.string(80),
                    'start': c.u32(),
                    'end': c.u32(),
                    'moveSpeed': c.f32(),
                    'flags': c.u32(),
                    'rarity': c.f32(),
                    'syncPoint': c.u32(),
                    'radius': c.f32(),
                    'min': c.floats(3),
                    'max': c.floats(3),
                }
                out['sequences'].append(seq)
        elif tag == b'TEXS':
            while c.more():
                out['textures'].append({
                    'replaceableId': c.u32(),
                    'path': c.string(260),
                    'flags': c.u32(),
                })
        elif tag == b'MTLS':
            while c.more():
                mstart = c.p
                msize = c.u32()
                mat = {'priority': c.i32(), 'flags': c.u32(), 'layers': []}
                if out['version'] >= 900:
                    mat['shader'] = c.string(80)
                assert c.tag() == b'LAYS'
                layers = c.u32()
                for _ in range(layers):
                    lstart = c.p
                    lsize = c.u32()
                    layer = {
                        'filterMode': c.u32(),
                        'shadingFlags': c.u32(),
                        'textureId': c.i32(),
                        'textureAnimId': c.i32(),
                        'coordId': c.u32(),
                        'alpha': c.f32(),
                        'emissive': c.f32(),
                    }
                    c.p = lstart + lsize
                    mat['layers'].append(layer)
                c.p = mstart + msize
                out['materials'].append(mat)
        elif tag == b'GEOS':
            while c.more():
                gstart = c.p
                gsize = c.u32()
                g = {}
                assert c.tag() == b'VRTX'
                g['vertices'] = [c.floats(3) for _ in range(c.u32())]
                assert c.tag() == b'NRMS'
                g['normals'] = [c.floats(3) for _ in range(c.u32())]
                assert c.tag() == b'PTYP'
                g['faceTypes'] = [c.u32() for _ in range(c.u32())]
                assert c.tag() == b'PCNT'
                g['faceGroups'] = [c.u32() for _ in range(c.u32())]
                assert c.tag() == b'PVTX'
                g['faces'] = [c.u16() for _ in range(c.u32())]
                assert c.tag() == b'GNDX'
                g['vertexGroups'] = [c.u8() for _ in range(c.u32())]
                assert c.tag() == b'MTGC'
                g['matrixGroups'] = [c.u32() for _ in range(c.u32())]
                assert c.tag() == b'MATS'
                g['matrixIndices'] = [c.u32() for _ in range(c.u32())]
                g['materialId'] = c.u32()
                g['selectionGroup'] = c.u32()
                g['selectionFlags'] = c.u32()
                if out['version'] > 800:
                    g['lod'] = c.i32()
                    g['lodName'] = c.string(80)
                g['bounds'] = (c.f32(), c.floats(3), c.floats(3))
                g['extents'] = [(c.f32(), c.floats(3), c.floats(3)) for _ in range(c.u32())]
                nxt = c.tag()
                if nxt == b'TANG':
                    g['tangents'] = [c.floats(4) for _ in range(c.u32())]
                    nxt = c.tag()
                if nxt == b'SKIN':
                    n = c.u32()
                    raw = c.d[c.p:c.p + n]
                    c.p += n
                    g['skin'] = [(list(raw[i:i + 4]), list(raw[i + 4:i + 8])) for i in range(0, n, 8)]
                    nxt = c.tag()
                assert nxt == b'UVAS', nxt
                sets = c.u32()
                g['uvs'] = []
                for _ in range(sets):
                    assert c.tag() == b'UVBS'
                    g['uvs'].append([c.floats(2) for _ in range(c.u32())])
                c.p = gstart + gsize
                out['geosets'].append(g)
        elif tag == b'BONE':
            while c.more():
                node = read_node(c)
                node['geosetId'] = c.i32()
                node['geosetAnimId'] = c.i32()
                out['bones'].append(node)
        elif tag == b'HELP':
            while c.more():
                out['helpers'].append(read_node(c))
        elif tag == b'ATCH':
            while c.more():
                start = c.p
                size2 = struct.unpack_from('<I', c.d, c.p)[0]
                node = read_node(c)
                c.p = start + size2
                out['attachments'].append(node)
        elif tag == b'PIVT':
            while c.more():
                out['pivots'].append(c.floats(3))
        else:
            pass
        r.p = end
    return out


if __name__ == '__main__':
    import sys
    m = parse(sys.argv[1])
    print('version', m['version'], 'name', m['name'])
    print('textures:')
    for t in m['textures']:
        print('  ', t)
    print('materials:', len(m['materials']))
    for mat in m['materials']:
        print('  ', mat)
    print('geosets:', len(m['geosets']))
    for i, g in enumerate(m['geosets']):
        print('  %d verts=%d faces=%d mat=%d skin=%s uvsets=%d matrixGroups=%d'
              % (i, len(g['vertices']), len(g['faces']) // 3, g['materialId'],
                 'yes' if 'skin' in g else 'no', len(g['uvs']), len(g['matrixGroups'])))
    print('bones:', len(m['bones']), 'helpers:', len(m['helpers']), 'pivots:', len(m['pivots']))
    print('sequences:', len(m['sequences']))
    for s in m['sequences']:
        print('   %-22s %6d-%6d moveSpeed=%.1f flags=%d' % (s['name'], s['start'], s['end'], s['moveSpeed'], s['flags']))
    tk = sum(len(t['keys']) for b in m['bones'] for t in b['tracks'].values())
    print('bone keyframes:', tk)
    print('bone sample:', {k: v for k, v in m['bones'][0].items() if k != 'tracks'},
          {k: (v['interp'], len(v['keys'])) for k, v in m['bones'][0]['tracks'].items()})
