using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Memory;
using Elsa.Workflows.Models;

namespace ElsaServer.Activities;

public class PromotionValidationWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Promotion Validation Workflow";
        builder.Description = "電商複雜促銷規則驗收：平行執行 API 算錢與 UI 渲染檢查，最後人工簽核。";

        // API 分支變數
        var apiResult = new Variable<object>();

        builder.Root = new Sequence
        {
            Variables = { apiResult },
            Activities =
            {
                // 1. Trigger
                new HttpEndpoint
                {
                    Path = new Input<string>("/webhooks/validate-promotion"),
                    SupportedMethods = new Input<ICollection<string>>(new[] { "POST" }),
                    CanStartWorkflow = true
                },

                // 設定 CorrelationId 以便後續 HttpEndpoint 喚醒
                new Inline(context => 
                {
                    context.WorkflowExecutionContext.CorrelationId = context.WorkflowExecutionContext.Id;
                    return ValueTask.CompletedTask;
                }),

                new Inline(context => 
                {
                    context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("收到驗收請求，開始平行測試...");
                    return ValueTask.CompletedTask;
                }),

                // 2. Parallel 平行驗證 (會等待所有分支完成)
                new Elsa.Workflows.Activities.Parallel
                {
                    Activities =
                    {
                        // 路線 A: API 邏輯驗證
                        new Sequence
                        {
                            Activities =
                            {
                                new Inline(context => 
                                {
                                    context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("路線 A：執行 API 算錢驗證");
                                    return ValueTask.CompletedTask;
                                }),
                                new SendHttpRequest
                                {
                                    Url = new Input<Uri>(new Uri("https://localhost:7238/api/cart/calculate")),
                                    Method = new Input<string>("POST"),
                                    ParsedContent = new Output<object>(apiResult)
                                },
                                new Inline(context => 
                                {
                                    var logger = context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>();
                                    try 
                                    {
                                        var res = apiResult.Get(context);
                                        logger.LogInformation("API 原始回傳結果: {Result}", System.Text.Json.JsonSerializer.Serialize(res));
                                    } 
                                    catch (Exception ex) 
                                    {
                                        logger.LogError(ex, "API 結果解析發生錯誤");
                                    }
                                    return ValueTask.CompletedTask;
                                }),
                                new If
                                {
                                    Condition = new Input<bool>(context => 
                                    {
                                        try
                                        {
                                            var result = apiResult.Get(context);
                                            if (result is System.Text.Json.JsonElement json)
                                            {
                                                return json.GetProperty("finalPrice").GetDecimal() == 980m;
                                            }
                                            
                                            // 處理 ExpandoObject 或其他動態型別 (可能被反序列化為 double, long 等)
                                            dynamic dynResult = result;
                                            var priceVal = dynResult?.finalPrice;
                                            if (priceVal != null)
                                            {
                                                return Convert.ToDecimal(priceVal) == 980m;
                                            }
                                            return false;
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"API 解析錯誤: {ex.Message}");
                                            return false;
                                        }
                                    }),
                                    Then = new Inline(context => 
                                    {
                                        context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("✅ 路線 A 通過：API 算錢正確 (980元)");
                                        return ValueTask.CompletedTask;
                                    }),
                                    Else = new Sequence
                                    {
                                        Activities = 
                                        {
                                            new Inline(context => 
                                            {
                                                context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogWarning("🚨 [RPA] API 算錢驗證失敗！預期 980 元，但後端回傳錯誤結果。");
                                                return ValueTask.CompletedTask;
                                            }),
                                            new SendErrorEmailActivity
                                            {
                                                To = new Input<string>("xx@gmail.com"),
                                                Subject = new Input<string>("🚨 [RPA 警報] 促銷活動 API 算錢驗證異常！"),
                                                Body = new Input<string>("您好：\n\n系統在自動驗收促銷活動時，發現後端 API 算錢結果錯誤 (預期 980 元)。\n請工程團隊盡速檢查伺服器邏輯！\n\n系統將自動中斷本次發佈流程。")
                                            },
                                            new Fault { Message = new("API 算錢驗證失敗") }
                                        }
                                    }
                                }
                            }
                        },

                        // 路線 B: 前端 UI 驗證
                        new Sequence
                        {
                            Activities =
                            {
                                new Inline(context => 
                                {
                                    context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("路線 B：啟動 Playwright 進行前端 UI 驗收");
                                    return ValueTask.CompletedTask;
                                }),
                                new StartPlaywrightSessionActivity
                                {
                                    Url = new(context => $"file:///{System.IO.Path.GetFullPath("playwright_crawler/cart.html").Replace("\\", "/")}"),
                                    Headless = new(false) // 設定為 false 方便觀察，若在伺服器上跑可改回 true
                                },
                                new PlaywrightExecuteScriptActivity
                                {
                                    // 填入優惠碼並點擊套用，等待一下讓 UI 更新
                                    InteractionScript = new(@"
await Page.Locator(""#couponCode"").FillAsync(""PROMO100"");
await Page.Locator(""#applyCouponBtn"").ClickAsync();
await System.Threading.Tasks.Task.Delay(1000);
Logger.LogInformation(""已輸入優惠碼並套用"");
")
                                },
                                new PlaywrightSnapshotActivity
                                {
                                    // 截取全螢幕
                                },
                                new ClosePlaywrightSessionActivity()
                            }
                        }
                    }
                },

                // 3. 會合後處理 (Join 等待兩者都完成)
                new Inline(context => 
                {
                    context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("✅ 平行驗證完成，準備寄送簽核信件");
                    return ValueTask.CompletedTask;
                }),

                new SendApprovalEmailActivity
                {
                    To = new Input<string>("ss@gmail.com")
                },

                new Inline(context => 
                {
                    context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("等待 PM 簽核中...");
                    return ValueTask.CompletedTask;
                }),

                // 4. 等待人工簽核 (使用 HttpEndpoint)
                new HttpEndpoint
                {
                    Path = new Input<string>("/api/approve-promotion"),
                    SupportedMethods = new Input<ICollection<string>>(new[] { "GET" }),
                    CanStartWorkflow = false
                },

                new Inline(context => 
                {
                    context.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PromotionValidationWorkflow>>().LogInformation("PM 已簽核！");
                    return ValueTask.CompletedTask;
                }),

                new WriteHttpResponse
                {
                    Content = new Input<object>("<meta charset='utf-8'><h1>工作流驗收已成功！</h1><p>您可以關閉此視窗並前往 Telegram 查看最終通知。</p>"),
                    ContentType = new Input<string>("text/html"),
                    StatusCode = new Input<System.Net.HttpStatusCode>(System.Net.HttpStatusCode.OK)
                }

                // 5. 最終發佈通知
                /*new TelegramBotActivity
                {
                    Token = new Input<string>("YOUR_TELEGRAM_BOT_TOKEN"),
                    UserId = new Input<string>("YOUR_TELEGRAM_CHAT_ID"),
                    Message = new Input<string>("🎉 [RPA] 促銷功能已由 PM 驗收完畢並核准上線，活動隨時可啟動！")
                }*/
            }
        };
    }
}
