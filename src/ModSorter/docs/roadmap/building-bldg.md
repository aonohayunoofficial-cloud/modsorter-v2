# 建築物 / 建物（`building` / `bldg`）

親: [../ROADMAP.md](../ROADMAP.md) ｜ 進捗: 12/12 完了

箱ベース（幅・奥行き・高さ・屋根形式）。共通パラメータと屋根形式マスターは
[building-common.md](building-common.md) にある。

## 小分類

- [x] 戸建て住宅(`house`) / [x] 集合住宅(`apartment`) / [x] 工場(`factory`) / [x] 研究所(`laboratory`)
- [x] 倉庫(`warehouse`) / [x] 店舗・商業施設(`shop`) / [x] オフィスビル(`office`) / [x] 宗教建築(`religious`)
- [x] 塔(`tower`) / [x] 記念建築(`monument`) / [x] 屋外イベント会場(`event_venue`) / [x] 公共施設(`public_facility`)

## パラメータUI（`ManualCatalog.Building.cs` の factory）

| 小分類 | UserControl |
|---|---|
| 戸建て住宅 | `HouseParamsControl` |
| 集合住宅 | `ApartmentParamsControl` |
| 工場 | `FactoryParamsControl` |
| 研究所 | `LaboratoryParamsControl` |
| 倉庫 | `WarehouseParamsControl` |
| 店舗・商業施設 | `ShopParamsControl` |
| オフィスビル | `OfficeParamsControl` |
| 宗教建築 | `ReligiousParamsControl` |
| 塔 | `TowerParamsControl` |
| 記念建築 | `MonumentParamsControl` |
| 屋外イベント会場 | `EventVenueParamsControl` |
| 公共施設 | `PublicFacilityParamsControl` |

## 残課題

- 屋根形式の追加・大型シャッターは小分類の網羅とは別枠の継続項目
  （[building-common.md](building-common.md)）。
