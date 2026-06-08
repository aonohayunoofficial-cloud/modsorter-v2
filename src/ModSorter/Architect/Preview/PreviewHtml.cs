namespace ModSorter.Architect.Preview;

// 3Dプレビュー用のHTML（Three.js）を提供する。
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
          font-family:monospace; font-size:12px; }
</style>
<script src='https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.min.js'></script>
<script src='https://cdn.jsdelivr.net/npm/three@0.160.0/examples/js/controls/OrbitControls.js'></script>
</head>
<body>
<div id='info'>(生成するとここに表示されます)</div>
<script>
let scene, camera, renderer, controls, group;

function init() {
  scene = new THREE.Scene();
  scene.background = new THREE.Color(0x1c1c1c);

  camera = new THREE.PerspectiveCamera(
    50, window.innerWidth / window.innerHeight, 0.1, 1000);
  camera.position.set(12, 12, 12);

  renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setSize(window.innerWidth, window.innerHeight);
  document.body.appendChild(renderer.domElement);

  controls = new THREE.OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;

  scene.add(new THREE.AmbientLight(0xffffff, 0.6));
  const dir = new THREE.DirectionalLight(0xffffff, 0.8);
  dir.position.set(10, 20, 10);
  scene.add(dir);

  window.addEventListener('resize', onResize);
  animate();
}

function onResize() {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
}

function animate() {
  requestAnimationFrame(animate);
  controls.update();
  renderer.render(scene, camera);
}

// ブロックIDごとに色を割り当てる（簡易ハッシュ）
function colorFor(id) {
  const known = {
    'minecraft:oak_planks': 0xc8a564,
    'minecraft:oak_log':    0x8a6a3b,
    'minecraft:glass':      0x88ccee,
    'minecraft:cobblestone':0x888888
  };
  if (known[id] !== undefined) return known[id];
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) & 0xffffff;
  return h;
}

// C#から呼ばれる。blocks = [{x,y,z,id}, ...]
function renderBlocks(json) {
  const blocks = JSON.parse(json);
  if (group) { scene.remove(group); }
  group = new THREE.Group();

  const geo = new THREE.BoxGeometry(1, 1, 1);
  const edgeGeo = new THREE.EdgesGeometry(geo);
  const edgeMat = new THREE.LineBasicMaterial({ color: 0x222222 });

  let minX=1e9,minY=1e9,minZ=1e9,maxX=-1e9,maxY=-1e9,maxZ=-1e9;

  for (const b of blocks) {
    const isGlass = (b.id === 'minecraft:glass');
    const mat = new THREE.MeshLambertMaterial({
      color: colorFor(b.id),
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

  // 構造の中心が原点に来るよう平行移動
  const cx = (minX+maxX)/2, cy = (minY+maxY)/2, cz = (minZ+maxZ)/2;
  group.position.set(-cx, -cy, -cz);
  scene.add(group);

  // 大きさに応じてカメラ距離を調整
  const span = Math.max(maxX-minX, maxY-minY, maxZ-minZ, 4);
  const d = span * 1.8 + 4;
  camera.position.set(d, d, d);
  controls.target.set(0, 0, 0);
  controls.update();

  document.getElementById('info').textContent = blocks.length + ' blocks';
}

init();
</script>
</body>
</html>";
    }
}
