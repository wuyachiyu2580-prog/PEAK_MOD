"""Inspect binary FBX structure and extract embedded images; never modifies input."""
import argparse
import array
import collections
import hashlib
import json
import pathlib
import struct
import sys
import zlib


class Fbx:
    def __init__(self, path):
        self.data = path.read_bytes()
        if not self.data.startswith(b'Kaydara FBX Binary  \x00\x1a\x00'):
            raise ValueError('Expected binary FBX')
        self.version = struct.unpack_from('<I', self.data, 23)[0]
        self.wide = self.version >= 7500
        self.null_size = 25 if self.wide else 13
        self.pos = 27
        self.nodes = []
        while True:
            node = self.node()
            if node is None:
                break
            self.nodes.append(node)

    def unpack(self, fmt):
        result = struct.unpack_from('<' + fmt, self.data, self.pos)
        self.pos += struct.calcsize('<' + fmt)
        return result

    def prop(self):
        kind = chr(self.data[self.pos])
        self.pos += 1
        scalar = {'Y': 'h', 'C': '?', 'I': 'i', 'F': 'f', 'D': 'd', 'L': 'q'}
        if kind in scalar:
            return self.unpack(scalar[kind])[0]
        if kind in ('S', 'R'):
            size, = self.unpack('I')
            value = self.data[self.pos:self.pos + size]
            self.pos += size
            return value.decode('utf-8', 'replace') if kind == 'S' else value
        if kind in 'fdlibc':
            count, encoding, size = self.unpack('III')
            value = self.data[self.pos:self.pos + size]
            self.pos += size
            if encoding == 1:
                value = zlib.decompress(value)
            elif encoding != 0:
                raise ValueError('Unknown array encoding')
            result = array.array({'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'B', 'c': 'B'}[kind])
            result.frombytes(value)
            if sys.byteorder != 'little':
                result.byteswap()
            if len(result) != count:
                raise ValueError('Array length mismatch')
            return result
        raise ValueError('Unknown property type ' + kind)

    def node(self):
        end, count, prop_len, name_len = self.unpack('QQQB' if self.wide else 'IIIB')
        if end == 0:
            return None
        if end > len(self.data):
            raise ValueError('Node extends beyond file')
        name = self.data[self.pos:self.pos + name_len].decode('utf-8', 'replace')
        self.pos += name_len
        prop_start = self.pos
        props = [self.prop() for _ in range(count)]
        if self.pos - prop_start != prop_len:
            raise ValueError('Property block length mismatch')
        children = []
        while self.pos < end:
            child = self.node()
            if child is None:
                break
            children.append(child)
        if self.pos != end:
            raise ValueError('Node end mismatch: ' + name)
        return {'name': name, 'props': props, 'children': children}


def child(node, name):
    return next((c for c in node['children'] if c['name'] == name), None)


def value(node, name, default=None):
    item = child(node, name)
    return item['props'][0] if item and item['props'] else default


def properties(node):
    p = child(node, 'Properties70')
    return {c['props'][0]: c['props'][4:] for c in p['children'] if c['name'] == 'P'} if p else {}


def clean_name(s):
    return str(s).split('\x00')[0]


def inspect(source, output):
    fbx = Fbx(source)
    root = {'children': fbx.nodes}
    objects = child(root, 'Objects')['children']
    report = {'input': str(source.resolve()), 'bytes': len(fbx.data),
              'sha256': hashlib.sha256(fbx.data).hexdigest(), 'fbx_version': fbx.version,
              'global_settings': properties(child(root, 'GlobalSettings')),
              'object_counts': dict(collections.Counter(n['name'] for n in objects)),
              'models': [], 'meshes': [], 'deformers': [], 'textures': [], 'materials': [], 'embedded_images': []}
    connections = child(root, 'Connections')
    report['connections'] = [n['props'] for n in connections['children'] if n['name'] == 'C'] if connections else []
    output.mkdir(parents=True, exist_ok=True)
    for n in objects:
        info = {'id': n['props'][0], 'name': clean_name(n['props'][1]), 'type': n['props'][2]}
        kind = n['name']
        if kind == 'Model':
            info['properties'] = properties(n)
            report['models'].append(info)
        elif kind == 'Geometry' and info['type'] == 'Mesh':
            vertices = value(n, 'Vertices', [])
            indices = value(n, 'PolygonVertexIndex', [])
            sizes = []
            size = 0
            for i in indices:
                size += 1
                if i < 0:
                    sizes.append(size)
                    size = 0
            info.update(vertices=len(vertices)//3, polygons=len(sizes),
                        triangles=sum(max(0, s-2) for s in sizes),
                        local_bounds=[[min(vertices[i::3]), max(vertices[i::3])] for i in range(3)] if vertices else [],
                        layers=[c['name'] for c in n['children'] if c['name'].startswith('LayerElement')])
            report['meshes'].append(info)
        elif kind == 'Deformer':
            info.update(indices=len(value(n, 'Indexes', [])), weights=len(value(n, 'Weights', [])))
            report['deformers'].append(info)
        elif kind == 'Material':
            info['properties'] = properties(n)
            report['materials'].append(info)
        elif kind == 'Texture':
            info.update(filename=value(n, 'FileName'), relative_filename=value(n, 'RelativeFilename'))
            report['textures'].append(info)
        elif kind == 'Video':
            content = value(n, 'Content', b'')
            filename = value(n, 'Filename', '') or value(n, 'RelativeFilename', '')
            if content:
                basename = pathlib.PureWindowsPath(filename).name or str(info['id']) + '.bin'
                dest = output / 'textures' / basename
                dest.parent.mkdir(parents=True, exist_ok=True)
                if dest.exists() and dest.read_bytes() != content:
                    raise ValueError('Conflicting extracted file: ' + str(dest))
                if not dest.exists():
                    dest.write_bytes(content)
                info.update(filename=filename, extracted=str(dest.resolve()), bytes=len(content),
                            sha256=hashlib.sha256(content).hexdigest())
                from PIL import Image
                with Image.open(dest) as im:
                    info.update(image_size=list(im.size), image_mode=im.mode)
                report['embedded_images'].append(info)
    report['summary'] = {'mesh_count': len(report['meshes']),
                         'vertices': sum(m['vertices'] for m in report['meshes']),
                         'polygons': sum(m['polygons'] for m in report['meshes']),
                         'triangles': sum(m['triangles'] for m in report['meshes']),
                         'bone_nodes': sum(m['type'] == 'LimbNode' for m in report['models']),
                         'skin_deformers': sum(d['type'] == 'Skin' for d in report['deformers']),
                         'clusters': sum(d['type'] == 'Cluster' for d in report['deformers']),
                         'embedded_images': len(report['embedded_images'])}
    path = output / 'fbx-inspection.json'
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'report': str(path.resolve()), 'summary': report['summary'],
                      'models': [{k:m[k] for k in ['id','name','type']} for m in report['models']],
                      'settings': report['global_settings']}, ensure_ascii=True))
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('input', type=pathlib.Path)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    inspect(args.input, args.output)
