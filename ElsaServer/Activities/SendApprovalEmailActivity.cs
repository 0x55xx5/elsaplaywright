using System.Net;
using System.Net.Mail;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Human", DisplayName = "Send Approval Email", Description = "寄送帶有 Playwright 截圖與簽核連結的 Email。")]
public class SendApprovalEmailActivity : CodeActivity<bool>
{
    [Input(Description = "收件人信箱 (例如 pm@company.com)")]
    public Input<string> To { get; set; } = default!;

    [Input(Description = "SMTP 伺服器 (選填)。如果沒有填寫，僅會在 Console 印出預覽信件，不會真正寄出。")]
    public Input<string> SmtpHost { get; set; } = default!;

    [Input(Description = "SMTP Port (例如 587)")]
    public Input<int> SmtpPort { get; set; } = new(587);

    [Input(Description = "寄件人信箱與登入帳號")]
    public Input<string> SmtpUser { get; set; } = default!;

    [Input(Description = "SMTP 密碼 或 App Password")]
    public Input<string> SmtpPassword { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<SendApprovalEmailActivity>>();
        var config = context.GetRequiredService<IConfiguration>();
        
        var instanceId = context.WorkflowExecutionContext.Id;
        var approveLink = $"https://localhost:7238/workflows/api/approve-promotion?correlationId={instanceId}";
        
        var subject = "✅ [Action Required] 促銷活動上線前驗收";
        var body = $"您好：\n\n系統已自動完成「促銷活動」的 API 算錢邏輯驗證與前端 UI 截圖擷取。\n\n" +
                   $"1. API 後端算錢：✅ 通過\n" +
                   $"2. 前端 UI 渲染：請查看信件附件的 Playwright 截圖。\n\n" +
                   $"確認畫面無誤後，請點擊下方連結核准上線：\n{approveLink}\n\n謝謝！";

        var toEmail = To?.Get(context) ?? "pm@example.com";
        var host = SmtpHost?.Get(context) ?? config["Smtp:Host"];
        var port = SmtpPort?.Get(context) > 0 ? SmtpPort.Get(context) : (int.TryParse(config["Smtp:Port"], out var p) ? p : 587);
        var user = SmtpUser?.Get(context) ?? config["Smtp:UserName"];
        var pass = SmtpPassword?.Get(context) ?? config["Smtp:Password"];

        byte[]? screenshotBytes = null;
        if (context.WorkflowExecutionContext.TransientProperties.TryGetValue("SharedPlaywrightImage", out var imgObj) && imgObj is byte[] bytes)
        {
            screenshotBytes = bytes;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("===============================================");
            logger.LogInformation("✉️ [模擬發信模式] 沒有配置 SMTP，信件預覽：");
            logger.LogInformation("To: {ToEmail}", toEmail);
            logger.LogInformation("Subject: {Subject}", subject);
            logger.LogInformation("Body: \n{Body}", body);
            if (screenshotBytes != null)
                logger.LogInformation("📎 附帶截圖大小: {Size} bytes", screenshotBytes.Length);
            logger.LogInformation("===============================================");
            context.SetResult(true);
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(user!),
                Subject = subject,
                Body = body
            };
            mailMessage.To.Add(toEmail!);

            if (screenshotBytes != null && screenshotBytes.Length > 0)
            {
                var stream = new MemoryStream(screenshotBytes);
                var attachment = new Attachment(stream, "Checkout_Screenshot.png", "image/png");
                mailMessage.Attachments.Add(attachment);
            }

            await client.SendMailAsync(mailMessage);
            logger.LogInformation("✅ 簽核信件已成功寄出至 {ToEmail}", toEmail);
            logger.LogInformation("✅ 連結核准 {approveLink}", approveLink);
            context.SetResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "🚨 發信失敗：{Message}", ex.Message);
            context.JournalData.Add("Error", ex.Message);
            context.SetResult(false);
        }
    }
}
