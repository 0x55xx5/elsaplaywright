using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.System", DisplayName = "Set Self Correlation ID", Description = "將當前工作流的 ID 綁定為喚醒辨識碼 (CorrelationId)，不需填寫任何參數。")]
public class SetSelfCorrelationIdActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<SetSelfCorrelationIdActivity>>();
        
        // 將這條流程的身分證字號，指派給喚醒辨識碼
        context.WorkflowExecutionContext.CorrelationId = context.WorkflowExecutionContext.Id;
        
        logger.LogInformation("✅ 已成功綁定喚醒辨識碼 (CorrelationId): {Id}", context.WorkflowExecutionContext.Id);
        
        return ValueTask.CompletedTask;
    }
}
