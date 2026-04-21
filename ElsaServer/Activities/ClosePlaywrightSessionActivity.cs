using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Vision", DisplayName = "Close Playwright Session", Description = "關閉並清理共用的 Playwright 資源。")]
public class ClosePlaywrightSessionActivity : Activity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<ClosePlaywrightSessionActivity>>();

        try
        {
            if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedPage", out var pageObj) && pageObj is IPage page)
            {
                await page.CloseAsync();
                context.WorkflowExecutionContext.TransientProperties.Remove("SharedPage");
            }
            
            if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedBrowser", out var browserObj) && browserObj is IBrowser browser)
            {
                await browser.DisposeAsync();
                context.WorkflowExecutionContext.TransientProperties.Remove("SharedBrowser");
            }

            if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedPlaywright", out var pwObj) && pwObj is IPlaywright playwright)
            {
                playwright.Dispose();
                context.WorkflowExecutionContext.TransientProperties.Remove("SharedPlaywright");
            }

            logger.LogInformation("Playwright Session 清理完畢");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清理 Playwright Session 時發生錯誤: {Message}", ex.Message);
            context.JournalData.Add("Error", ex.Message);
        }
    }
}
