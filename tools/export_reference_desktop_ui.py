"""只读提取用户提供参考包中的独立UI贴图，不改原包，不启动Unity。"""
from pathlib import Path
import sys, struct, json, hashlib, uuid
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
WORK=ROOT/'work/card-cultivation-handoff'
sys.path.insert(0,str(WORK))
import extract_r01 as parser
from unpack_unityfs import unpack

original=parser.parse_tree_node
def read_node(nodes,start,reader):
    node=nodes[start]
    if node['type']!='TypelessData':return original(nodes,start,reader)
    end=start+1
    while end<len(nodes) and nodes[end]['level']>node['level']:end+=1
    n=struct.unpack_from('<I',reader.raw,reader.pos)[0];reader.pos+=4
    value=reader.raw[reader.pos:reader.pos+n];assert len(value)==n
    reader.pos+=n
    if node['meta']&0x4000:reader.align()
    return value,end
parser.parse_tree_node=read_node

source=ROOT/'Card Cultivation/CardCultivation_Data/StreamingAssets/UI/UISprites/Guide.dat'
encoded=source.read_bytes();key=bytes(encoded[i]^b'UnityFS\0'[i]for i in range(4));data=bytearray(encoded)
for lane in range(4):data[lane::4]=data[lane::4].translate(bytes(i^key[lane]for i in range(256)))
decoded=WORK/'decoded/UI/UISprites/Guide.bundle';decoded.parent.mkdir(parents=True,exist_ok=True);decoded.write_bytes(data)
result=unpack(decoded)
unpacked=WORK/'unpacked/UI/UISprites/Guide'
asset=parser.SerializedFile(next(p for p in unpacked.iterdir()if not p.name.endswith('.resS')))
dest=ROOT/'Assets/IslandSurvival/Art/ReferenceUI';dest.mkdir(parents=True,exist_ok=True)
for folder in [dest.parent,dest]:
    meta=folder.with_name(folder.name+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n',encoding='utf8')
guid_map={'card_slot':'c7a8100cb2674801b96a909d3cbfc321','card_select':'a6f4ceba673741a5bf3e7cff02cb8842','timer_progress':'f8c48055781b4e5baa7cbd18943373a3'}
manifest=[]
for name,guid in guid_map.items():
    path='assets/gamemain/ui/uisprites/guide/'+name+'.png';pid=asset.container[path]
    sprite=asset.deserialize_object(pid);texture_id=sprite['m_RD']['texture']['pathID'];texture=asset.deserialize_object(texture_id)
    assert texture['m_TextureFormat']==4 and texture['m_MipCount']==1,texture['m_TextureFormat']
    stream=texture['m_StreamData'];media=unpacked/Path(stream['path']).name
    with media.open('rb')as file:file.seek(stream['offset']);pixels=file.read(stream['size'])
    width,height=texture['m_Width'],texture['m_Height'];assert len(pixels)==width*height*4
    output=dest/(name+'.png')
    Image.frombytes('RGBA',(width,height),pixels).transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(output)
    output.with_suffix('.png.meta').write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  textureType: 8
  textureShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {uuid.uuid5(uuid.NAMESPACE_URL,path).hex}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData: 用户授权参考UI复用，来源见来源.json
  assetBundleName:
  assetBundleVariant:
''',encoding='utf8')
    manifest.append({'asset':path,'sprite_pathID':pid,'texture_pathID':texture_id,'size':[width,height],
                     'output':output.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(output.read_bytes()).hexdigest()})
(dest/'来源.json').write_text(json.dumps({'source':source.relative_to(ROOT).as_posix(),
    'source_sha256':hashlib.sha256(encoded).hexdigest(),'usage':'用户授权先复用原版UI，后续通过Prefab/主题替换；不作为原创资产。',
    'assets':manifest},ensure_ascii=False,indent=2),encoding='utf8')
print('已导出参考UI',len(manifest),'项')
