using System.Collections.Immutable;
using System.Linq;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
namespace ElsaServer.Activities;

public class PlaywrightScriptGlobals
{
    public required IPage Page { get; set; }
    public required ILogger Logger { get; set; }
}

[Activity(Category = "RPA.Vision", DisplayName = "Playwright Execute Script", Description = "以動態編譯 C# 執行前置的 Playwright 腳本操作。包含編譯校驗。")]
public class PlaywrightExecuteScriptActivity : CodeActivity<bool>
{
    [Input(
        Description = "C# 腳本 (支援 await Page.XXX)。這段程式碼會在事前先經過編譯檢查。",
        UIHint = InputUIHints.MultiLine)]
    public Input<string> InteractionScript { get; set; } = default!;

    [Output(Description = "如果有錯誤會將編譯或執行錯誤輸出於此")]
    public Output<string> ErrorMessage { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<PlaywrightExecuteScriptActivity>>();
        var scriptCode = InteractionScript.Get(context);

        if (string.IsNullOrWhiteSpace(scriptCode))
        {
             logger.LogInformation("無腳本需要執行");
             context.SetResult(true);
             return;
        }

        if (!context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedPage", out var pageObj) || !(pageObj is IPage page))
        {
            var msg = "找不到已建立的 Playwright Page。請確保之前已執行 Start Playwright Session 節點。";
            logger.LogError(msg);
            SetErrorResult(context, msg);
            return;
        }

        try
        {
             logger.LogInformation("開始校驗並編譯腳本...");
             var scriptOptions = ScriptOptions.Default
                  .AddReferences(typeof(IPage).Assembly, typeof(ILogger).Assembly)
                  .AddImports("System", "System.Threading.Tasks", "Microsoft.Playwright", "Microsoft.Extensions.Logging");

             var script = CSharpScript.Create(scriptCode, scriptOptions, typeof(PlaywrightScriptGlobals));
             var diagnostics = script.GetCompilation().GetDiagnostics();
             
             // 過濾只抓 Error 層級 (編譯錯誤)
             var errors = diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
             if (errors.Any())
             {
                 var errorStr = string.Join("\n", errors.Select(e => e.GetMessage()));
                 var fullErrorPattern = $"腳本編譯失敗:\n{errorStr}";
                 logger.LogError(fullErrorPattern);
                 
                 // 將錯誤傳回 Elsa Studio UI (Journal & Output)
                 SetErrorResult(context, fullErrorPattern);
                 return;
             }

             // 編譯成功，執行腳本
             logger.LogInformation("腳本編譯無誤，開始執行...");
             var globals = new PlaywrightScriptGlobals { Page = page, Logger = logger };
             await script.RunAsync(globals);
             
             logger.LogInformation("腳本執行完畢。");
             context.SetResult(true);
        }
        catch (CompilationErrorException ex)
        {
             var msg = $"編譯例外錯誤: {ex.Message}";
             logger.LogError(msg);
             SetErrorResult(context, msg);
        }
        catch (PlaywrightException pex)
        {
             var msg = $"Playwright 作業錯誤: {pex.Message}";
             logger.LogError(msg);
             SetErrorResult(context, msg);
        }
        catch (Exception ex)
        {
             var msg = $"腳本執行期間發生未預期錯誤: {ex.Message}";
             logger.LogError(ex, msg);
             SetErrorResult(context, msg);
        }
    }

    private void SetErrorResult(ActivityExecutionContext context, string message)
    {
        context.JournalData.Add("Error", message);
        context.Set(ErrorMessage, message);
        context.SetResult(false);
    }
}
