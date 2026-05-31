using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Human", DisplayName = "Wait For Approval", Description = "暫停工作流，等待 PM 透過 API 呼叫喚醒 (Approve)。")]
public class ApprovePromotionEvent : Trigger
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        if (context.IsTriggerOfWorkflow())
        {
            await context.CompleteActivityAsync();
            return;
        }

        // 建立書籤，等待外部 API 喚醒
        context.CreateBookmark("ApprovePromotion");
    }

    protected override object GetTriggerPayload(TriggerIndexingContext context)
    {
        return "ApprovePromotion";
    }
}
