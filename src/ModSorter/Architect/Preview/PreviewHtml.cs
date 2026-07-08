namespace ModSorter.Architect.Preview;

// 3Dプレビュー用のHTML（Three.js）を提供する。
// OrbitControlsには依存せず、カメラ操作は自前実装（バージョン事故回避）。
// ブロックデータは描画後に window.renderBlocks(json) で渡す。
public static class PreviewHtml
{
    public static string Build()
    {
        return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  html, body { margin:0; padding:0; height:100%; overflow:hidden; background:#1c1c1c; }
  #info { position:absolute; top:6px; left:8px; color:#9fd39f;
          font-family:monospace; font-size:12px; pointer-events:none; }
  canvas { display:block; }
</style>
<script src='https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.min.js'></script>
</head>
<body>
<div id='info'>(初期化中...)</div>
<script>
window.onerror = function(msg) {
  document.getElementById('info').textContent = 'JSエラー: ' + msg;
};

let scene, camera, renderer, group;
// 自前カメラ制御の状態（球面座標：方位角yaw・仰角pitch・距離dist）
let yaw = 0.8, pitch = 0.6, dist = 20;
let dragging = false, lastX = 0, lastY = 0;

function init() {
  // CDN からの Three.js 取得が間に合わないことがあるため、
  // THREE が現れるまで待ってからリトライする。
  if (typeof THREE === 'undefined') {
    document.getElementById('info').textContent =
      'THREE 読込待ち...';
    setTimeout(init, 100);
    return;
  }

  scene = new THREE.Scene();
  scene.background = new THREE.Color(0x1c1c1c);

  camera = new THREE.PerspectiveCamera(
    50, window.innerWidth / window.innerHeight, 0.1, 1000);

  renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setSize(window.innerWidth, window.innerHeight);
  document.body.appendChild(renderer.domElement);

  scene.add(new THREE.AmbientLight(0xffffff, 0.6));
  const dir = new THREE.DirectionalLight(0xffffff, 0.8);
  dir.position.set(10, 20, 10);
  scene.add(dir);

  // ===== 自前のマウス操作 =====
  const el = renderer.domElement;
  el.addEventListener('mousedown', (e) => {
    dragging = true; lastX = e.clientX; lastY = e.clientY;
  });
  window.addEventListener('mouseup', () => { dragging = false; });
  window.addEventListener('mousemove', (e) => {
    if (!dragging) return;
    yaw   -= (e.clientX - lastX) * 0.01;
    pitch -= (e.clientY - lastY) * 0.01;
    pitch = Math.max(-1.5, Math.min(1.5, pitch)); // 真上・真下を超えない
    lastX = e.clientX; lastY = e.clientY;
  });
  el.addEventListener('wheel', (e) => {
    e.preventDefault();
    dist *= (e.deltaY > 0) ? 1.1 : 0.9;
    dist = Math.max(3, Math.min(200, dist));
  }, { passive: false });

  window.addEventListener('resize', onResize);
  document.getElementById('info').textContent = '(生成するとここに表示されます)';
  animate();
}

function onResize() {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
}

function updateCamera() {
  // 球面座標から直交座標へ。target は原点（構造は原点中心に寄せる）。
  const cp = Math.cos(pitch), sp = Math.sin(pitch);
  const cy = Math.cos(yaw),   sy = Math.sin(yaw);
  camera.position.set(dist * cp * sy, dist * sp, dist * cp * cy);
  camera.lookAt(0, 0, 0);
}

function animate() {
  requestAnimationFrame(animate);
  updateCamera();
  renderer.render(scene, camera);
}

function colorFor(id) {
  const known = {
    'minecraft:oak_planks':  0xc8a564,
    'minecraft:oak_log':     0x8a6a3b,
    'minecraft:glass':       0x88ccee,
    'minecraft:cobblestone': 0x888888,
    'minecraft:oak_stairs':  0xb89456,
    'minecraft:stone_bricks':0x9a9a9a,
    'minecraft:stone':       0x9a9a9a
  };
  if (known[id] !== undefined) return known[id];
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) & 0xffffff;
  return h;
}

// ---- テクスチャ管理（texKey 単位）----
// texKey は baseId(例 create:shaft) でも、面別テクスチャ参照(例 create:block/shaft_side)
// でもよい。C#から _texMap[texKey] = dataURI で渡される。
const _texCache = {};   // texKey -> THREE.Texture (or null)
const _matCache = {};   // texKey -> THREE.Material
let _texMap = {};       // texKey -> dataURI

function getTexture(texKey) {
  if (_texCache[texKey] !== undefined) return _texCache[texKey];
  const uri = _texMap[texKey];
  if (!uri) { _texCache[texKey] = null; return null; }
  const tex = new THREE.TextureLoader().load(uri);
  tex.magFilter = THREE.NearestFilter;
  tex.minFilter = THREE.NearestFilter;
  tex.colorSpace = THREE.SRGBColorSpace;
  _texCache[texKey] = tex;
  return tex;
}

// 単一マテリアル（elements 無しブロック用。baseId のテクスチャ or 単色）。
function materialFor(baseId) {
  if (_matCache[baseId] !== undefined) return _matCache[baseId];
  const isGlass = (baseId === 'minecraft:glass');
  const tex = getTexture(baseId);
  let mat;
  if (tex) {
    mat = new THREE.MeshLambertMaterial({
      map: tex, transparent: isGlass, opacity: isGlass ? 0.5 : 1.0
    });
  } else {
    mat = new THREE.MeshLambertMaterial({
      color: colorFor(baseId), transparent: isGlass, opacity: isGlass ? 0.4 : 1.0
    });
  }
  _matCache[baseId] = mat;
  return mat;
}

// texKey 用の面マテリアル（テクスチャがあればそれ、無ければ baseId フォールバック）。
// キャッシュキーは texKey。UVはジオメトリ側で面ごとに調整するので、
// マテリアル自体は texKey 単位で共有してよい。
function faceMaterialFor(texKey, baseId) {
  const key = texKey || ('base:' + baseId);
  if (_matCache[key] !== undefined) return _matCache[key];
  let tex = texKey ? getTexture(texKey) : null;
  if (!tex && baseId) tex = getTexture(baseId); // baseId フォールバック
  let mat;
  if (tex) {
    mat = new THREE.MeshLambertMaterial({ map: tex });
  } else {
    mat = new THREE.MeshLambertMaterial({ color: colorFor(baseId || texKey || '') });
  }
  _matCache[key] = mat;
  return mat;
}

// BoxGeometry の面グループ順は [+x, -x, +y, -y, +z, -z]。
// Minecraft の面名 east=+x, west=-x, up=+y, down=-y, south=+z, north=-z。
const FACE_ORDER = ['east','west','up','down','south','north'];

// faces の1面ぶんから texKey(文字列)を取り出す。
// C#は {tex,uv,rot} オブジェクトで送るが、念のため文字列でも受けられるようにする。
function faceTexKey(fi) {
  if (!fi) return null;
  if (typeof fi === 'string') return fi;
  return (typeof fi.tex === 'string') ? fi.tex : null;
}

// 面別マテリアル配列(6要素)を返す。faces が無い面は baseId 全面。
function faceMaterials(elFaces, baseId) {
  const mats = [];
  for (const fn of FACE_ORDER) {
    const fi = (elFaces && elFaces[fn]) ? elFaces[fn] : null;
    const texKey = faceTexKey(fi);
    mats.push(faceMaterialFor(texKey, baseId));
  }
  return mats;
}

// C#から呼ばれる。textures(任意) = { texKey: dataURI, ... }
function setTextures(json) {
  try { _texMap = json ? JSON.parse(json) : {}; }
  catch (e) { _texMap = {}; }
  // テクスチャが入れ替わったらキャッシュを捨てて作り直す。
  for (const k in _texCache) delete _texCache[k];
  for (const k in _matCache) delete _matCache[k];
}

// 面のUVを Minecraft の uv([x1,y1,x2,y2] 0..16) と rotation で BoxGeometry に適用する。
// BoxGeometry の UV 属性は面ごとに4頂点。faceIndex*4 が開始インデックス。
// three r160 の BoxGeometry 各面の頂点順は [左上, 右上, 左下, 右下]。
function applyFaceUV(geo, faceIndex, uv, rot) {
  const uvAttr = geo.attributes.uv;
  const base = faceIndex * 4;

  // Minecraft uv は 0..16。テクスチャ座標は 0..1、V は上下反転。
  const u0 = uv[0] / 16, u1 = uv[2] / 16;
  const v0 = uv[1] / 16, v1 = uv[3] / 16;
  // three の V は下が0・上が1。MinecraftのUVは上が0なので V を反転する。
  const tv0 = 1 - v0, tv1 = 1 - v1;

  // 左=u0,右=u1,上=tv0,下=tv1。頂点順 [左上,右上,左下,右下]。
  let corners = [
    [u0, tv0], // 左上
    [u1, tv0], // 右上
    [u0, tv1], // 左下
    [u1, tv1]  // 右下
  ];

  // rotation(0/90/180/270): 面テクスチャを回す = 割り当て順をローテーション。
  const r = ((rot % 360) + 360) % 360;
  if (r === 90) {
    corners = [corners[2], corners[0], corners[3], corners[1]];
  } else if (r === 180) {
    corners = [corners[3], corners[2], corners[1], corners[0]];
  } else if (r === 270) {
    corners = [corners[1], corners[3], corners[0], corners[2]];
  }

  for (let i = 0; i < 4; i++) {
    uvAttr.setXY(base + i, corners[i][0], corners[i][1]);
  }
  uvAttr.needsUpdate = true;
}

// uv 省略時の Minecraft 既定UV。面ごとに from/to の該当2軸を切り出す。
function defaultUVForFace(fn, from, to) {
  const x0 = from[0], y0 = from[1], z0 = from[2];
  const x1 = to[0],   y1 = to[1],   z1 = to[2];
  switch (fn) {
    case 'up':    return [x0, z0, x1, z1];
    case 'down':  return [x0, z0, x1, z1];
    case 'north': return [16 - x1, 16 - y1, 16 - x0, 16 - y0];
    case 'south': return [x0, 16 - y1, x1, 16 - y0];
    case 'west':  return [z0, 16 - y1, z1, 16 - y0];
    case 'east':  return [16 - z1, 16 - y1, 16 - z0, 16 - y0];
    default:      return [0, 0, 16, 16];
  }
}

// element の from/to(0..16)と faces から、UV適用済みの BoxGeometry を作る。
// UVは各面ごとに違う可能性があるためキャッシュせず都度生成する
// (小箱数はブロックあたり十数個程度で許容範囲)。
function boxGeoForElement(sx, sy, sz, elFaces, from, to) {
  const geo = new THREE.BoxGeometry(sx, sy, sz);
  for (let fidx = 0; fidx < FACE_ORDER.length; fidx++) {
    const fn = FACE_ORDER[fidx];
    const fi = (elFaces && elFaces[fn]) ? elFaces[fn] : null;
    if (!fi) continue; // 指定が無い面は既定UVのまま

    let uv = (typeof fi === 'object' && fi.uv && fi.uv.length >= 4) ? fi.uv : null;
    if (!uv) uv = defaultUVForFace(fn, from, to);
    const rot = (typeof fi === 'object' && typeof fi.rot === 'number') ? fi.rot : 0;
    applyFaceUV(geo, fidx, uv, rot);
  }
  return geo;
}

// ---- ジオメトリ・キャッシュ（従来方式ブロック用。サイズ単位でBox/Edgeを使い回す）----
const _geoCache = {};
function geoFor(sx, sy, sz) {
  const key = sx.toFixed(3) + 'x' + sy.toFixed(3) + 'x' + sz.toFixed(3);
  if (!_geoCache[key]) {
    const g = new THREE.BoxGeometry(sx, sy, sz);
    _geoCache[key] = { box: g, edge: new THREE.EdgesGeometry(g) };
  }
  return _geoCache[key];
}

// エッジ線用マテリアル（従来方式ブロックの境界表示に使う）。
const _edgeMat = new THREE.LineBasicMaterial({ color: 0x222222 });

// 3軸名から Vector3 を返す。
function axisVec(s) {
  if (s === 'y') return new THREE.Vector3(0,1,0);
  if (s === 'z') return new THREE.Vector3(0,0,1);
  return new THREE.Vector3(1,0,0);
}

// 1ブロック分の描画。elements があれば複数の小箱(面別テクスチャ+UV)で、
// 無ければ従来の単一箱(baseIdテクスチャ)で描く。
// elements 時はエッジ線を描かない（薄い小箱が多数で黒線だらけになるため）。
function addBlock(b, bbox) {
  try {
    const baseId = b.id.split('[')[0];

    if (Array.isArray(b.elements) && b.elements.length > 0) {
      // ---- elements 方式 ----
      const rx = (typeof b.rotX === 'number') ? b.rotX : 0;
      const ry = (typeof b.rotY === 'number') ? b.rotY : 0;
      const rz = (typeof b.rotZ === 'number') ? b.rotZ : 0;
      const needRot = (rx !== 0 || ry !== 0 || rz !== 0);
      const container = needRot ? new THREE.Group() : group;

      for (const el of b.elements) {
        const f = el.from, t = el.to;
        if (!f || !t) continue;
        const sx = Math.max((t[0] - f[0]) / 16, 0.0001);
        const sy = Math.max((t[1] - f[1]) / 16, 0.0001);
        const sz = Math.max((t[2] - f[2]) / 16, 0.0001);

        const lcx = ((f[0] + t[0]) / 2) / 16 - 0.5;
        const lcy = ((f[1] + t[1]) / 2) / 16 - 0.5;
        const lcz = ((f[2] + t[2]) / 2) / 16 - 0.5;

        // UV適用済みジオメトリ。faces と from/to を渡す。
        const geo = boxGeoForElement(sx, sy, sz, el.faces, f, t);
        const mats = faceMaterials(el.faces, baseId);
        const mesh = new THREE.Mesh(geo, mats);

        // 要素の1段回転(モデルJSONの element.rotation 相当)。
        // 原点(0..16px)中心で pivot を作り、その周りに小箱を回す。
        if (typeof el.rotAngle === 'number' && el.rotAngle !== 0) {
          const ox = (el.rotOrigin ? el.rotOrigin[0] : 8)/16 - 0.5;
          const oy = (el.rotOrigin ? el.rotOrigin[1] : 8)/16 - 0.5;
          const oz = (el.rotOrigin ? el.rotOrigin[2] : 8)/16 - 0.5;

          const pivot = new THREE.Object3D();
          pivot.rotateOnAxis(axisVec(el.rotAxis), el.rotAngle * Math.PI/180);

          mesh.position.set(lcx - ox, lcy - oy, lcz - oz);
          pivot.add(mesh);

          if (needRot) { pivot.position.set(ox, oy, oz); container.add(pivot); }
          else { pivot.position.set(b.x + ox, b.y + oy, b.z + oz); group.add(pivot); }
          continue;
        }

        if (needRot) {
          mesh.position.set(lcx, lcy, lcz);
        } else {
          mesh.position.set(b.x + lcx, b.y + lcy, b.z + lcz);
        }
        container.add(mesh);
        // elements 時はエッジ線を描かない(黒線だらけ防止)。
      }

      if (needRot) {
        const toRad = Math.PI / 180;
        // Create の BeltRenderer は Y→Z→X の順で座標系を積む
        // (msr.rotateYDegrees→rotateZDegrees→rotateXDegrees)。
        // three.js の Euler は order 文字列の逆順に適用されるため、
        // Y→Z→X を再現するには order='XZY'。符号は左手系→右手系で反転。
        container.rotation.order = 'XZY';
        container.rotation.x = -rx * toRad;
        container.rotation.y = -ry * toRad;
        container.rotation.z = -rz * toRad;
        container.position.set(b.x, b.y, b.z);
        group.add(container);
      }
    } else {
      // ---- 従来方式（後方互換）。sx/sy/sz、無ければ 1×1×1 ----
      const sx = (typeof b.sx === 'number' && b.sx > 0) ? b.sx : 1;
      const sy = (typeof b.sy === 'number' && b.sy > 0) ? b.sy : 1;
      const sz = (typeof b.sz === 'number' && b.sz > 0) ? b.sz : 1;
      const g = geoFor(sx, sy, sz);

      const mesh = new THREE.Mesh(g.box, materialFor(baseId));
      mesh.position.set(b.x, b.y, b.z);
      group.add(mesh);

      const edges = new THREE.LineSegments(g.edge, _edgeMat);
      edges.position.set(b.x, b.y, b.z);
      group.add(edges);
    }
  } catch (err) {
    const info = document.getElementById('info');
    if (info) info.textContent = 'addBlockエラー(' + b.id + '): ' + err.message;
  }

  if (b.x < bbox.minX) bbox.minX = b.x;
  if (b.y < bbox.minY) bbox.minY = b.y;
  if (b.z < bbox.minZ) bbox.minZ = b.z;
  if (b.x > bbox.maxX) bbox.maxX = b.x;
  if (b.y > bbox.maxY) bbox.maxY = b.y;
  if (b.z > bbox.maxZ) bbox.maxZ = b.z;
}

// C#から呼ばれる。blocks = [{x,y,z,id, (elements|sx,sy,sz)}, ...]
function renderBlocks(json) {
  if (!scene) {
    document.getElementById('info').textContent = 'シーン準備待ち...（データ受領済）';
    setTimeout(function() { renderBlocks(json); }, 100);
    return;
  }
  const blocks = JSON.parse(json);
  if (group) { scene.remove(group); }
  group = new THREE.Group();

  const bbox = { minX:1e9,minY:1e9,minZ:1e9,maxX:-1e9,maxY:-1e9,maxZ:-1e9 };

  for (const b of blocks) {
    addBlock(b, bbox);
  }

  const cx = (bbox.minX+bbox.maxX)/2;
  const cy = (bbox.minY+bbox.maxY)/2;
  const cz = (bbox.minZ+bbox.maxZ)/2;
  group.position.set(-cx, -cy, -cz);
  scene.add(group);

  const span = Math.max(bbox.maxX-bbox.minX, bbox.maxY-bbox.minY, bbox.maxZ-bbox.minZ, 4);
  dist = span * 1.8 + 4;

  document.getElementById('info').textContent = blocks.length + ' blocks';
}

init();
</script>
</body>
</html>";
    }
}
