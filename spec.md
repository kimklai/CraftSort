# CraftSort 專案規格與進度

## 專案概述

- **名稱：** CraftSort
- **目前版本：** 1.5.0
- **用途：** Valheim 用戶端模組，在製作站的配方清單旁加入排序／篩選按鈕。
- **遊戲與執行環境：** Valheim 0.221.13、Unity 6、BepInEx 5.4.x、Harmony。
- **目標框架：** .NET Framework 4.8；使用 .NET 8 SDK 建置。
- **程式庫識別：** `dev.craftsort`（詳見 `Plugin.cs`）。
- **伺服器需求：** 無；不加入伺服器端同步邏輯。
- **授權：** AGPL-3.0。

## 功能規格

### 配方清單排序與篩選

- 以 Harmony transpiler 在 `InventoryGui.UpdateRecipeList` 的原版配方定位迴圈前，對可用配方排序及篩選；排序後由原版程式依新順序顯示。
- Transpiler 需將目標 IL 指令上的 labels 搬至注入程式碼的第一個指令，並匹配預期的最後一個配方列表迴圈。
- AAA Crafting 相容補丁在分頁前排序 `RecipeListPerfCache.CraftSortedFiltered` 的複本，不修改 AAA Crafting 的原始快取。
- 排序模式包含原版順序、護甲、格擋、物理傷害、斬擊、穿刺、鈍擊、劈砍、火焰、冰霜、閃電、毒、靈魂、生命、耐力、魔力、名稱與新配方。
- 武器類型篩選包含單手與雙手；篩選先於排序模式套用，可與傷害排序並用。雙手篩選包含雙手武器、弓與 Atgeir 附件類型。
- 排序值依數值由大至小排列；同值時保留原列表順序。名稱模式使用本地化名稱排序。
- 每次排序都需對可能為 null 的 `Recipe`、`ItemData`、`SharedData` 等資料安全處理。

### 食物與蜜酒回復排序

- 生命、耐力、魔力排序以對應的食物數值與消耗效果數值計分。
- 烹飪桌原料須依 `CookingStation.m_conversion` 找到烹煮後成品，再以成品數值排序；蜜酒原料須依 `Fermenter.m_conversion` 找到發酵後成品。
- 成品快取以原料 prefab 名稱索引，並在排序系統重設時清除。
- 對應的 `SE_Stats` 即時恢復量與持續恢復量均納入一般回復分數。
- 若效果持續時間至少 60 秒，且該排序項目對應的回復倍率大於 1，給予 `1,000,000` 的優先分數，再加上倍率與一般回復分數。此類長時效回復增益因此排在直接回血／回復耐力／回復魔力前；各排序只依其對應屬性判斷。
- 不對食物項目使用額外的食物類型排序加成，以免扭曲實際回復數值。

### 製作站介面

- 依配方內容、ObjectDB 配方及站名後備判斷食物／戰鬥工作站；包含 Cauldron、Food Preparation Table 與 Fermenter（蜜酒鍋）。
- 食物面板以單欄排列，戰鬥面板以可調欄數的網格排列。兩面板各自保存位置與寬度。
- 按鈕可由設定逐一啟用或停用。排序按鈕使用圖示；當前排序以黃色外框表示，武器篩選以藍色外框表示。
- 拖曳與調整大小需先透過角落控制柄右鍵開啟編輯模式；面板設定儲存在 `[UI]` 設定區。
- 清除按鈕將已知配方標記為已檢視並移除新配方標記。

### 新配方追蹤

- 依角色存檔持久化已檢視配方，資料寫入 `BepInEx/config/CraftSort/viewed_{name}.txt`。
- 首次初始化時，將角色已知配方快照為已檢視；`resetknownitems` 指令會清除追蹤資料並同步清理目前列表標記。
- 配方被選取後標記為已檢視；未檢視配方在圖示上顯示標記，也能使用 New 模式篩選。

### 安全熱載入

- 支援透過 BepInEx.Debug ScriptEngine 載入 `BepInEx/scripts` 中的 DLL，並以遊戲內 F6 觸發重新載入。
- Plugin 銷毀時需解除自身 Harmony patches、UI 物件、事件處理器及執行期圖示／快取資源，避免重複載入殘留。
- 重新載入後重新開啟製作介面以重建 UI。
- 同一個 Plugin GUID 不可同時放置於 `plugins` 與 `scripts` 的兩份 DLL。

## 架構與主要檔案

| 檔案 | 職責 |
|---|---|
| `CraftSort/Plugin.cs` | BepInEx 入口、設定、Harmony 啟動與卸載清理 |
| `CraftSort/SortLogic.cs` | 排序模式、排序分數、武器篩選、成品轉換與排序快取 |
| `CraftSort/Patches.cs` | 排序 transpiler、AAA Crafting 相容、新配方標記、檢視狀態與重設指令補丁 |
| `CraftSort/TabUI.cs` | 分類面板、按鈕、拖曳／縮放、工作站偵測與按鈕設定 |
| `CraftSort/NewRecipeTracker.cs` | 角色配方檢視狀態、新配方標記與持久化 |
| `CraftSort/IconFactory.cs` | 由嵌入式 RGBA 資料建立及清除 Sprite 快取 |
| `CraftSort/IconData.cs` | 以 Base64 儲存的圖示像素資料 |
| `CraftSort/CraftSort.csproj` | .NET Framework 4.8 專案、遊戲參考及建置後部署 |

## 建置與部署

- 必須安裝 .NET 8 SDK。
- `VALHEIM_INSTALL` 環境變數指定 Valheim 安裝目錄，供遊戲／Unity／BepInEx 參考及部署路徑共用。
- 未設定 `VALHEIM_INSTALL` 時，專案仍以 `C:\Program Files (x86)\Steam\steamapps\common\Valheim` 為預設安裝目錄。
- 一般建置命令：

  ```powershell
  $env:VALHEIM_INSTALL = 'E:\SteamLibrary\steamapps\common\Valheim'
  dotnet build CraftSort\CraftSort.csproj -c Debug
  ```

- 支援 Debug 與 Release 組態；Release 建置命令為 `dotnet build CraftSort\CraftSort.csproj -c Release`。
- 輸出路徑為 `CraftSort/bin/{Configuration}/net48/`，兩種組態都產生 DLL 與 portable PDB。
- 建置成功後自動建立並複製 `CraftSort.dll` 與 `CraftSort.pdb` 至 `$(VALHEIM_INSTALL)/BepInEx/scripts`，供 ScriptEngine 熱載入與除錯資訊解析。
- 專案參考的 BepInEx DLL 來源為同一安裝目錄下的 `BepInEx/core`；Valheim 與 Unity 組件來源為 `valheim_Data/Managed`。
- 建置輸出與遊戲部署由專案 target 完成；部署目的地需可寫入。

## 相容性與限制

- 目前專案說明列出的遊戲版本為 0.221.13（Unity 6）。
- InventorySlots 會整體替換製作介面，與 CraftSort 不相容。
- 任何 Unity／Valheim／BepInEx 欄位或 Harmony 目標變更，都需對照目前安裝的組件確認。
- 熱載入與完整遊戲內排序結果仍需透過實際遊戲工作階段驗證；成功編譯不能代替遊戲內驗證。

## 目前進度

- 已完成：食物排序依烹煮後數值；蜜酒依發酵後成品的生命、耐力、魔力回復數值排序。
- 已完成：至少 60 秒且對應回復倍率大於 1 的長效回復效果取得最高優先級。
- 已完成：ScriptEngine 熱載入清理流程及 F6 重新載入支援。
- 已完成：建置後部署目標改為使用 `VALHEIM_INSTALL` 動態決定安裝位置，並複製至該目錄下的 `BepInEx/scripts`。
- 最近一次以 `E:\SteamLibrary\steamapps\common\Valheim` 建置及部署成功，0 個警告、0 個錯誤。
- 以上程式改動已提交並推送至 `origin/main`；此文件建立後須另行提交與推送。
