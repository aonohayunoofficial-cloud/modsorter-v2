using System.Collections.Generic;

namespace ModSorter.Architect.Generation;

// structure_type による特殊形状ビルダーへの振り分け。
// ここで非 null を返した時点で ExpandCore は通常の床/壁/屋根/開口部・入口保証を
// 一切通さずに終わる。判定の順序は元の if 連鎖のままなので、
// "bridge"（完全一致・Civil.cs の簡易橋）と "bridge:"（BridgeExpander）の
// 前後関係も従来どおり。
public static partial class StructureExpander
{
    // 特殊形状なら座標を返す。通常の建物（"building" など）なら null を返す。
    private static List<GeneratedBlock>? TryBuildSpecial(
        StructureSpec spec, string structureType,
        int w, int d, int h,
        IReadOnlyList<string> allowedBlocks, string fallback)
    {
        if (structureType == "ramp")
        {
            string rampBody = Pick(spec.WallBlock ?? spec.FloorBlock, allowedBlocks, fallback);
            string rampBase = Pick(spec.BaseBlock ?? spec.FloorBlock ?? spec.WallBlock, allowedBlocks, rampBody);
            return BuildRamp(w, d, h, rampBody, rampBase, spec.RidgeAxis);
        }
        if (structureType == "bridge")
        {
            string deckBlock = Pick(spec.WallBlock ?? spec.FloorBlock, allowedBlocks, fallback);
            string pierBlock = Pick(spec.BaseBlock ?? spec.WallBlock ?? spec.FloorBlock, allowedBlocks, deckBlock);
            return BuildBridge(w, d, h, deckBlock, pierBlock, spec.RidgeAxis);
        }
        if (structureType == "ship")
        {
            // 船は ShipExpander が船体・甲板・上部構造物・出入口をすべて確定的に作る。
            // 床/壁/屋根/開口部・入口保証は一切通さない（出入口は船種ごとに自動配置）。
            return ShipExpander.Build(spec, w, d, h, allowedBlocks, fallback);
        }
        if (structureType == "venue")
        {
            // 屋外会場は VenueExpander が観客席・フィールド・シェル・テントを確定的に作る。
            // 高さ1の板も段状の客席も VenueExpander 側で直接置くので、
            // Clamp(height,2,64) や平屋根の生成には一切かからない。
            return VenueExpander.Build(spec, allowedBlocks, fallback);
        }
        if (PublicFacilityExpander.Handles(structureType))
        {
            // 公共施設（"civic:" 接頭辞）は PublicFacilityExpander が体育館・病院・消防署・
            // 市庁舎を確定的に作る。競技面や病棟の内部間仕切りまで専用ビルダー側で置くので、
            // 通常の床/壁/屋根/開口部・入口保証は一切通さない。
            return PublicFacilityExpander.Build(spec, allowedBlocks, fallback);
        }
        if (HarborExpander.Handles(structureType))
        {
            // 港湾の単体構造物（"harbor:" 接頭辞）は HarborExpander が岸壁・桟橋・防波堤を
            // 確定的に作る。断面は harbor_* から組み、捨石マウンドや防舷材が z<0 へ張り出す
            // ぶんも HarborExpander 側で 0 起点へ正規化するので、通常の展開は一切通さない。
            return HarborExpander.Build(spec, allowedBlocks, fallback);
        }
        if (AirportExpander.Handles(structureType))
        {
            // 空港の平面土木施設（"airport:" 接頭辞）は AirportExpander が滑走路・誘導路・
            // エプロンを確定的に作る。舗装は y=0 の 1 層で、標識は同じ層の塗り分け、
            // 縁灯だけ y=1 に載る。ショルダーが負座標へ張り出すぶんも AirportExpander 側で
            // 0 起点へ正規化するので、通常の床/壁/屋根の展開は一切通さない。
            return AirportExpander.Build(spec, allowedBlocks, fallback);
        }
        if (RailwayExpander.Handles(structureType))
        {
            // 鉄道（"railway:" 接頭辞）は RailwayExpander がプラットフォームなどを
            // 確定的に作る。線路・道床・ホーム躯体が負座標へ張り出すぶんも
            // RailwayExpander 側で 0 起点へ正規化するので、通常の展開は一切通さない。
            return RailwayExpander.Build(spec, allowedBlocks, fallback);
        }
        if (BridgeExpander.Handles(structureType))
        {
            // 橋梁（"bridge:" 接頭辞）は BridgeExpander が桁橋などを確定的に作る。
            // 上の structureType == "bridge"（Civil.cs の簡易橋）は完全一致判定なので
            // "bridge:girder_bridge" はここまで落ちてくる。既存の簡易橋には影響しない。
            // 橋台の取付部が z<0 へ張り出すぶんも BridgeExpander 側で 0 起点へ
            // 正規化するので、通常の床/壁/屋根/開口部の展開は一切通さない。
            return BridgeExpander.Build(spec, allowedBlocks, fallback);
        }

        if (IndustryExpander.Handles(structureType))
        {
            // 産業インフラ（"industry:" 接頭辞）は IndustryExpander が縦型容器などを
            // 確定的に作る。円筒・円錐・ドームは回転体として直接焼くので、矩形前提の
            // 床/壁/屋根や「非矩形フットプリントでは屋根が flat に落ちる」制約に
            // かからない。防油堤や基礎パッドが負座標へ張り出すぶんも IndustryExpander
            // 側で 0 起点へ正規化する。
            return IndustryExpander.Build(spec, allowedBlocks, fallback);
        }

        return null;
    }
}
