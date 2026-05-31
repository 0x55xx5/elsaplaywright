# Elsa Studio 畫布實戰：手拉「自動化驗收平行工作流」全攻略

這份教學將帶您在 Elsa Studio (`https://localhost:7123`) 的畫布中，將原本用 C# 撰寫的 `PromotionValidationWorkflow`，原汁原味地透過「拖曳與連線」重現出來！

> [!TIP]
> **畫布操作小訣竅**：
> 1. 從左側 **Toolbox (工具箱)** 拖曳節點到畫布中。
> 2. 點擊節點後，在右側的 **Properties (屬性面板)** 填寫參數。
> 3. 從節點下方的「點 (Port)」拉出線條，連接到下一個節點的頂部。

---

## 🚀 階段一：工作流觸發起點

### 1. HttpEndpoint (接收驗收請求)
- **搜尋分類**：`HTTP` -> `HttpEndpoint`
- **參數設定 (Properties)**：
  - **Path**：`/webhooks/validate-promotion`
  - **Supported Methods**：勾選 `POST`
  - *(註：在 Elsa 3 的 UI 中，只要這個節點是「沒有上方連線」的第一個節點，系統就會自動將它當成觸發器 (Trigger)，不一定會顯示 `CanStartWorkflow` 這個勾選框)*

### 1.5 Set Self Correlation ID (設定喚醒辨識碼)
> [!IMPORTANT]
> 這是非常關鍵的一步！因為我們後面的簽核網址會帶上 `correlationId`，所以我們必須在一開頭就把流程的身份證綁定好！
- **搜尋分類**：`RPA.System` -> `Set Self Correlation ID`
- **連線**：從第一個 `HttpEndpoint` 連到這個節點。
- **參數設定 (Properties)**：**完全不用填寫任何參數！**
  *(這是我剛剛專門為您客製化的超級防呆節點，它會在底層自動把這條工作流本身的 ID 設為它的喚醒辨識碼，絕不報錯！)*

---

## 🔀 階段二：開啟平行宇宙

### 2. Parallel (平行處理)
- **搜尋分類**：`Control Flow` -> `Parallel`
- **連線**：將上方的 `HttpEndpoint` 連到 `Parallel`。
- **參數設定 (Properties)**：不需要特別設定，畫布上會自動長出分支 (Branch)。您可以拉出兩條線，分別作為「路線 A」與「路線 B」。

---

## 🅰️ 路線 A：API 後端驗證

在 `Parallel` 的左邊分支線下方，依序串聯以下節點：

### 3. SendHttpRequest (打測試 API)
- **搜尋分類**：`HTTP` -> `SendHttpRequest`
- **參數設定 (Properties)**：
  - **Url**：`https://localhost:7238/api/cart/calculate`
  - **Method**：`POST`
- **進階設定 (Advanced)**：
  - 在 **Parsed Content** 欄位，您可以宣告一個變數 (例如命名為 `apiResult`) 來接住回傳的 JSON，這樣後面的 If 才能判斷。

### 4. If (判斷算錢結果)
- **搜尋分類**：`Control Flow` -> `If`
- **參數設定 (Properties)**：
  - **Condition**：切換到 `JavaScript` 模式，填入：
    ```javascript
    getVariable("apiResult").finalPrice === 980
    ```
  *(此節點底部會長出 True 和 False 兩個分支)*

### 5. Send Error Email (失敗警報)
- **搜尋分類**：`RPA.Notification` -> `Send Error Email`
- **連線**：將 `If` 的 **False** 分支連到這個節點。
- **參數設定 (Properties)**：
  - **To**：`您的信箱@company.com`
  - **Subject**：`🚨 [RPA 警報] 促銷活動 API 算錢驗證異常！`
  - **Body**：`系統發現後端算錢結果錯誤，請盡速檢查！`

### 6. Fault (中斷流程)
- **搜尋分類**：`Primitives` -> `Fault`
- **連線**：將 `Send Error Email` 往下連到 `Fault`。
- **參數設定 (Properties)**：
  - **Message**：`API 算錢驗證失敗`

---

## 🅱️ 路線 B：前端 Playwright 爬蟲

在 `Parallel` 的右邊分支線下方，依序串聯以下我們自製的專屬節點：

### 7. Start Playwright Session (啟動瀏覽器)
- **搜尋分類**：`RPA.Vision` -> `Start Playwright Session`
- **參數設定 (Properties)**：
  - **Url**：填入您本機 HTML 的絕對路徑，例如：
    `file:///C:/Users/Kevin/Documents/elsaplaywright/ElsaServer/playwright_crawler/cart.html`
  - **Headless**：`false` *(設為 false 才能看到瀏覽器跳出來展示)*

### 8. Playwright Execute Script (執行爬蟲腳本)
- **搜尋分類**：`RPA.Vision` -> `Playwright Execute Script`
- **連線**：從 Start Playwright 連過來。
- **參數設定 (Properties)**：
  - **Interaction Script**：切換為一般文字，直接貼上 C# 裡面的爬蟲指令：
    ```csharp
    await Page.Locator("#couponCode").FillAsync("PROMO100");
    await Page.Locator("#applyCouponBtn").ClickAsync();
    await System.Threading.Tasks.Task.Delay(1000);
    ```

### 9. Close Playwright Session (關閉瀏覽器)
- **搜尋分類**：`RPA.Vision` -> `Close Playwright Session`
- **連線**：從 Execute Script 連過來。
- **參數設定 (Properties)**：無，它會在底層自動清理資源。

---

## 🤝 階段三：會合與真人簽核 (Human-in-the-loop)

> [!IMPORTANT]
> **如何會合？**
> 請把路線 A `If` 節點的 **True** 分支箭頭，以及路線 B `Close Playwright Session` 的向下箭頭，**同時拉去連接下一個節點 (Send Approval Email)**，這樣 Elsa 就會知道這裡需要 Join (等待兩邊都抵達才繼續)！

### 10. Send Approval Email (發送簽核信)
- **搜尋分類**：`RPA.Human` -> `Send Approval Email`
- **參數設定 (Properties)**：
  - **To**：`您的信箱@company.com`
  *(信件主旨和內文，我們已經在底層程式碼寫死了，所以 UI 上只要填收件人即可)*

### 11. HttpEndpoint (暫停！等待 PM 點擊網址)
- **搜尋分類**：`HTTP` -> `HttpEndpoint`
- **連線**：從發信節點連過來。
- **參數設定 (Properties)**：
  - **Path**：`/api/approve-promotion`
  - **Supported Methods**：勾選 `GET`
  - *(註：在 Elsa 3 UI 裡，只要您有把前一個節點「連線」到這個 HttpEndpoint，系統就會聰明地知道它是一個「等待點 (Bookmark)」，而不會把它當成起點，因此找不到 `CanStartWorkflow` 設定是正常的！)*

### 12. WriteHttpResponse (網頁成功訊息)
- **搜尋分類**：`HTTP` -> `WriteHttpResponse`
- **連線**：從等待簽核的 HttpEndpoint 連過來。
- **參數設定 (Properties)**：
  - **Content**：`<meta charset='utf-8'><h1>工作流驗收已成功！</h1><p>您可以關閉此視窗。</p>`
  - **Content Type**：`text/html`
  - **Status Code**：`OK (200)`

---

🎉 **大功告成！** 🎉
點擊右上角的 **Publish** (發佈)。這時候您就可以關掉原本用 C# 寫死的那隻腳本，改由這個用 UI 拉出來的動態腳本來接管所有驗收任務了！所有的參數和腳本，未來都可以直接在網頁上隨時修改，連重開機都不用！
