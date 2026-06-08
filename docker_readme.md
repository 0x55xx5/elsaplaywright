Viewed docker-compose.yml:1-31

這是一份為您的 Elsa Server 專案準備的 Docker 啟動與更新總結指南，您可以將這些指令記錄下來以便日後參考：

### 🚀 1. 首次啟動教學

只要您的電腦已開啟 Docker Desktop，就可以在專案根目錄（`elsadocker` 資料夾）的終端機執行：

```bash
# 啟動並在背景執行所有服務
docker-compose up -d
```
- **等待服務準備好**：首次啟動時，PostgreSQL 會有大約幾秒鐘的初始化時間，Elsa Server 啟動時也會自動建立資料表結構。
- **瀏覽網站**：打開瀏覽器前往 [http://localhost:5000](http://localhost:5000) 即可看到您的應用程式。

---

### 🔄 2. 修改代碼後如何更新？

由於 Docker 是將您的程式碼打包成映像檔 (Image)，所以**每次您修改了 C# 程式碼或設定檔（例如 Program.cs 或 appsettings.json）後，都需要重新建置映像檔**。

請直接執行這個加上 `--build` 參數的指令：

```bash
# 重新建置修改過的程式碼，並重啟容器
docker-compose up --build -d
```
> **💡 小提示**：這個指令非常聰明，它只會重新編譯有變動的 `elsa-server`，而您的 `postgres` 資料庫容器和裡面的流程資料會**完全保留不受影響**。

---

### 🛠️ 3. 其他日常實用指令

在開發過程中，您可能會經常使用到以下這三個指令：

**查看即時日誌 (Logs)**
如果伺服器出錯或您想看 Console 的輸出，可以使用：
```bash
# 查看 elsa-server 的即時日誌 (按 Ctrl+C 退出)
docker-compose logs -f elsa-server
```

**停止服務 (但保留資料)**
當您不開發時想關閉節省電腦資源：
```bash
# 停止並移除容器
docker-compose down
```

**⚠️ 毀滅指令 (徹底重置資料庫)**
如果您把系統改壞了，想要**清除所有資料**從零開始：
```bash
# 停止容器，並且刪除資料庫的 Volume (資料將會永遠消失！)
docker-compose down -v
```