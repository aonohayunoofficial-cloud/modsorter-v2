# プロペラ（`propeller`）

親: [../ROADMAP.md](../ROADMAP.md) ｜ 進捗: 0/10

単体オブジェクト。フェーズ7で着手。

## 小分類

### 中分類「枚数」(`blade_count`) 0/4
- [ ] 2枚羽(`blade2`)
- [ ] 3枚羽(`blade3`)
- [ ] 4枚羽(`blade4`)
- [ ] 5枚以上・多翼(`blade_multi`)

### 中分類「翼形状」(`blade_shape`) 0/3
- [ ] 後退翼(`swept`)
- [ ] 直線翼(`straight`)
- [ ] 曲線翼(`curved`)

### 中分類「機構」(`blade_mech`) 0/3
- [ ] 可変ピッチ翼(`variable_pitch`)
- [ ] ダクテッド・覆い付き(`ducted`)
- [ ] 二重反転(`contra_rotating`)

## パラメータ

枚数・全長・根元弦長・翼端弦長・後退角・ねじり(twist/pitch)・翼形状・素材・回転角

## 着手時の見込み

回転体・曲面の生成器が要る。産業（風車）で作った回転体の幾何
（`IndustryExpander.cs` の `InR`／`Disc`／`Ring`／`Revolve`）と、風車の垂直軸翼で
使った SLCA 近似・ソリディティの考え方がそのまま流用できる見込み。
`IndustryExpander.Face` と同じ canonical → facade_face 回転の枠に載せる。

## 実物研究と既定値

未着手。

## 残課題

（実装後に記入）
