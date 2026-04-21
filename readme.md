# 1. 
git clone https://github.com/0x55xx5/elsaplaywright.git
# 2. 
git clone https://github.com/0x55xx5/snapshottool.git


# 3. clean

```
dotnet add package SixLabors.ImageSharp
dotnet add package Microsoft.Playwright

dotnet tool install --global Microsoft.Playwright.CLI

playwright install
```

## 左邊clean正在進行的專案  右邊是完成的專案
### 設定email

```
dotnet add package Elsa.Email
```


https://myaccount.google.com/apppasswords

Program.cs

```
.UseEmail(email => email.ConfigureOptions = options => configuration.GetSection("Smtp").Bind(options))
```


C#

```


指定使用https
```
dotnet run --urls "https://localhost:7238"
```


```
        // Click the get started link.
 await Page.GetByRole(AriaRole.Link, new() { Name = "Get started" }).ClickAsync();
```

JS:

```
var result = getVariable("MyVerifiedResult");
 
// 多加一個 result 存在與否的判斷會更安全
if (result && result.IsMatch) {
    return "比對成功！相似度：" + result.Confidence;
} else {
    return "警報：畫面對比失敗！";
}
 
```