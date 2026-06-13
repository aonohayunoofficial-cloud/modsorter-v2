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

// テクスチャ(データURI)からマテリアルを作る。キャッシュ付き。
const _texCache = {};   // baseId -> THREE.Texture
const _matCache = {};   // baseId -> THREE.Material
let _texMap = {};       // baseId -> dataURI（C#から受領）
const _texLoader = (typeof THREE !== 'undefined') ? new THREE.TextureLoader() : null;

function getTexture(baseId) {
  if (_texCache[baseId] !== undefined) return _texCache[baseId];
  const uri = _texMap[baseId];
  if (!uri) { _texCache[baseId] = null; return null; }
  const tex = new THREE.TextureLoader().load(uri);
  // ブロックテクスチャはドット絵なので補間を切ってくっきり見せる。
  tex.magFilter = THREE.NearestFilter;
  tex.minFilter = THREE.NearestFilter;
  tex.colorSpace = THREE.SRGBColorSpace;
  _texCache[baseId] = tex;
  return tex;
}

function materialFor(baseId) {
  if (_matCache[baseId] !== undefined) return _matCache[baseId];
  const isGlass = (baseId === 'minecraft:glass');
  const tex = getTexture(baseId);
  let mat;
  if (tex) {
    // テクスチャあり。ガラスだけ半透明にする。
    mat = new THREE.MeshLambertMaterial({
      map: tex,
      transparent: isGlass, opacity: isGlass ? 0.5 : 1.0
    });
  } else {
    // テクスチャ無し（MOD未対応など）は従来の単色フォールバック。
    mat = new THREE.MeshLambertMaterial({
      color: colorFor(baseId),
      transparent: isGlass, opacity: isGlass ? 0.4 : 1.0
    });
  }
  _matCache[baseId] = mat;
  return mat;
}

// C#から呼ばれる。textures(任意) = { baseId: dataURI, ... }
function setTextures(json) {
  try {
    _texMap = json ? JSON.parse(json) : {};
  } catch (e) {
    _texMap = {};
  }
  // テクスチャが入れ替わったらキャッシュを捨てて作り直す。
  for (const k in _texCache) delete _texCache[k];
  for (const k in _matCache) delete _matCache[k];
}

// C#から呼ばれる。blocks = [{x,y,z,id}, ...]
function renderBlocks(json) {
  // scene がまだ無ければ、準備できるまで繰り返し待ってから描く。
  // (ウィンドウを開き直すたびに init が走るため、1回だけの待ちでは不足)
  if (!scene) {
    document.getElementById('info').textContent = 'シーン準備待ち...（データ受領済）';
    setTimeout(function() { renderBlocks(json); }, 100);
    return;
  }
  const blocks = JSON.parse(json);
  if (group) { scene.remove(group); }
  group = new THREE.Group();

  const geo = new THREE.BoxGeometry(1, 1, 1);
  const edgeGeo = new THREE.EdgesGeometry(geo);
  const edgeMat = new THREE.LineBasicMaterial({ color: 0x222222 });

  let minX=1e9,minY=1e9,minZ=1e9,maxX=-1e9,maxY=-1e9,maxZ=-1e9;

  for (const b of blocks) {
    // id に状態が付く場合がある（例: minecraft:oak_stairs[facing=north]）。
    // 色・ガラス判定は状態を剥がしたベースIDで行う。
    const baseId = b.id.split('[')[0];
    const mat = materialFor(baseId);
    const mesh = new THREE.Mesh(geo, mat);
    mesh.position.set(b.x, b.y, b.z);
    group.add(mesh);

    const edges = new THREE.LineSegments(edgeGeo, edgeMat);
    edges.position.set(b.x, b.y, b.z);
    group.add(edges);

    if (b.x<minX)minX=b.x; if (b.y<minY)minY=b.y; if (b.z<minZ)minZ=b.z;
    if (b.x>maxX)maxX=b.x; if (b.y>maxY)maxY=b.y; if (b.z>maxZ)maxZ=b.z;
  }

  const cx = (minX+maxX)/2, cy = (minY+maxY)/2, cz = (minZ+maxZ)/2;
  group.position.set(-cx, -cy, -cz);
  scene.add(group);

  // 大きさに応じて初期距離を調整
  const span = Math.max(maxX-minX, maxY-minY, maxZ-minZ, 4);
  dist = span * 1.8 + 4;

  document.getElementById('info').textContent = blocks.length + ' blocks';
}

init();
</script>
</body>
</html>";
    }
}
