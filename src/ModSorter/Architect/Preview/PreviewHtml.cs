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
  if (typeof THREE === 'undefined') {
    document.getElementById('info').textContent =
      'THREE 未読込（CDNに到達できていない可能性）';
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

// C#から呼ばれる。blocks = [{x,y,z,id}, ...]
function renderBlocks(json) {
  if (!scene) return;
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
    const isGlass = (baseId === 'minecraft:glass');
    const mat = new THREE.MeshLambertMaterial({
      color: colorFor(baseId),
      transparent: isGlass, opacity: isGlass ? 0.4 : 1.0
    });
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
