using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace ElsaServer.Activities;

/// <summary>
///  自動簽到 Activity
/// </summary>
[Activity("HAYO", "HAYO自動簽到", "執行自動簽到動作")]
public class ZzzAutoSignActivity : CodeActivity<string>
{
    // 共用單一 HttpClient 實例以避免 socket 耗盡
    private static readonly HttpClient _httpClient = new HttpClient();
    
    // 專用簽到 URL
    private const string SignUrl = "https://sg-hk4e-api.hoyolab.com/event/sol/sign?lang=zh-tw&act_id=e202102251931481";

    [Input(Description = "包含 ltoken_v2 與 ltuid_v2 的 Cookie 字串")]
    public Input<string> Token { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var token = Token.Get(context);
        
        using var request = new HttpRequestMessage(HttpMethod.Post, SignUrl);
        
        // 根據原腳本 headerDict 加入 Headers
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("x-rpc-app_version", "2.34.1");
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
        request.Headers.Add("x-rpc-client_type", "4");
        request.Headers.Add("Referer", "https://act.hoyolab.com/");
        request.Headers.Add("Origin", "https://act.hoyolab.com");
        request.Headers.Add("x-rpc-signgame", "hk4e"); //  Header
        
        // 帶入驗證用的 Cookie (Token)
        request.Headers.Add("Cookie", token);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(jsonResponse);
            var root = jsonDocument.RootElement;
            
            // 檢查是否受到圖形驗證碼 (Captcha) 阻擋 (responseJson.data?.gt_result?.is_risk)
            bool isRisk = false;
            if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object)
            {
                if (dataElement.TryGetProperty("gt_result", out var gtResultElement) && gtResultElement.ValueKind == JsonValueKind.Object)
                {
                    if (gtResultElement.TryGetProperty("is_risk", out var isRiskElement) && isRiskElement.ValueKind == JsonValueKind.True)
                    {
                        isRisk = true;
                    }
                }
            }

            if (isRisk)
            {
                context.SetResult(": 自動簽到失敗，受到圖形驗證阻擋。");
                return;
            }
            
            // 擷取一般回傳訊息 (例如 "OK" 或 "Traveler/Proxy, you've already checked in today")
            if (root.TryGetProperty("message", out var messageElement))
            {
                context.SetResult($": {messageElement.GetString()}");
                return;
            }

            context.SetResult(": 簽到成功，但無法解析回應訊息。");
        }
        catch (HttpRequestException httpEx)
        {
            context.SetResult($": HTTP 請求失敗 - {httpEx.Message}");
        }
        catch (JsonException jsonEx)
        {
            context.SetResult($": JSON 解析失敗 - {jsonEx.Message}");
        }
        catch (Exception ex)
        {
            context.SetResult($": 發生未預期的錯誤 - {ex.Message}");
        }
    }
    
}
