using System.Text.Json.Serialization;

namespace ModSorter.Architect.Generation;

// 屋外イベント会場（structure_type="venue"）のプロパティ。StructureSpec の partial。
// 客席の座面材 seat_block は産業（灯火）と共用なので共通ファイル側に置いてある。
public sealed partial class StructureSpec
{
    // ===== 屋外イベント会場（structure_type="venue"）=====
    // 会場の種類。"arena"（円形闘技場・コロッセウム式） | "stadium"（競技場） |
    // "bandshell"（野外音楽堂） | "stage"（ステージ） | "tents"（テント広場）。
    [JsonPropertyName("venue_kind")] public string? VenueKind { get; set; }

    // 客席の段数・踏面（1段の奥行）・蹴上（1段の高さ）。
    // コロッセウムの客席勾配は37度。踏面3・蹴上2 でおよそ34度になる。
    [JsonPropertyName("venue_rows")] public int? VenueRows { get; set; }
    [JsonPropertyName("venue_run")] public int? VenueRun { get; set; }
    [JsonPropertyName("venue_rise")] public int? VenueRise { get; set; }

    // ポディウム壁の高さ。競技面から最前列までの立ち上がり（コロッセウムは5m）。
    [JsonPropertyName("venue_podium")] public int? VenuePodium { get; set; }

    // 外周壁の立ち上がり。最上段からさらに上へ何マス積むか。
    // stage では背面の幕の高さ、片面スタンドでは背面壁の高さとして使う。
    [JsonPropertyName("venue_wall")] public int? VenueWall { get; set; }

    // 屋根（arena では日除け=velarium 相当）を張るか。既定 false。
    // コロッセウムに屋根は無いので、既定は必ず屋根なしにする。
    [JsonPropertyName("venue_roof")] public bool VenueRoof { get; set; }

    // 屋根の持ち上げ量。stage では柱の高さとして使う。
    [JsonPropertyName("venue_roof_height")] public int? VenueRoofHeight { get; set; }

    // 入場路（vomitoria）を客席の下に通すか。既定 false。
    [JsonPropertyName("venue_gates")] public bool VenueGates { get; set; }

    // 競技場の形式。"bowl"（既定・四周連続のボウル） | "single"（片面スタンド単体）。
    [JsonPropertyName("venue_sides")] public string? VenueSides { get; set; }

    // 野外音楽堂: オルケストラ（円形の平場）の半径。エピダウロスは径24.65m。
    [JsonPropertyName("venue_orchestra")] public int? VenueOrchestra { get; set; }

    // 野外音楽堂: シェルの平面半径と全高。ハリウッドボウルはシェル高45ft。
    [JsonPropertyName("venue_shell_radius")] public int? VenueShellRadius { get; set; }
    [JsonPropertyName("venue_shell_height")] public int? VenueShellHeight { get; set; }

    // 舞台の高さ。bandshell では演台、stage では床までの立ち上がり。
    [JsonPropertyName("venue_stage")] public int? VenueStage { get; set; }

    // テント広場: 張数・1張の間口と奥行・軒の高さ・テント間隔・列数・列間の通路幅。
    [JsonPropertyName("venue_tent_count")] public int? VenueTentCount { get; set; }
    [JsonPropertyName("venue_tent_w")] public int? VenueTentWidth { get; set; }
    [JsonPropertyName("venue_tent_d")] public int? VenueTentDepth { get; set; }
    [JsonPropertyName("venue_tent_eave")] public int? VenueTentEave { get; set; }
    [JsonPropertyName("venue_tent_gap")] public int? VenueTentGap { get; set; }
    [JsonPropertyName("venue_tent_rows")] public int? VenueTentRows { get; set; }
    [JsonPropertyName("venue_tent_aisle")] public int? VenueTentAisle { get; set; }

    // テントの側面を壁で塞ぐか。既定 false（柱だけの開放）。
    [JsonPropertyName("venue_tent_closed")] public bool VenueTentClosed { get; set; }

    // テントの下に地面を敷くか。既定 false（敷かないので床が二重にならない）。
    [JsonPropertyName("venue_tent_pave")] public bool VenueTentPave { get; set; }
}
