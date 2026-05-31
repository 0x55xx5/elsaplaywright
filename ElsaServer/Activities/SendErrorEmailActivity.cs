using System.Net;
using System.Net.Mail;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ElsaServer.Activities;

[Activity(Category = "RPA.Notification", DisplayName = "Send Error Email", Description = "寄送系統異常或驗證失敗的警告 Email。")]
public class SendErrorEmailActivity : CodeActivity<bool>
{
    [Input(Description = "收件人信箱")]
    public Input<string> To { get; set; } = default!;

    [Input(Description = "信件主旨")]
    public Input<string> Subject { get; set; } = default!;

    [Input(Description = "信件內容")]
    public Input<string> Body { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<SendErrorEmailActivity>>();
        var config = context.GetRequiredService<IConfiguration>();
        
        var subject = Subject?.Get(context) ?? "🚨 [RPA] 系統發生錯誤";
        var body = Body?.Get(context) ?? "流程執行時發生未預期的錯誤。";
        var toEmail = To?.Get(context) ?? "pm@example.com";

        var host = config["Smtp:Host"];
        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        var user = config["Smtp:UserName"];
        var pass = config["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("===============================================");
            logger.LogInformation("✉️ [模擬發信模式] 沒有配置 SMTP，錯誤通知信預覽：");
            logger.LogInformation("To: {ToEmail}", toEmail);
            logger.LogInformation("Subject: {Subject}", subject);
            logger.LogInformation("Body: \n{Body}", body);
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

            await client.SendMailAsync(mailMessage);
            logger.LogInformation("🚨 錯誤通知信已成功寄出至 {ToEmail}", toEmail);
            context.SetResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "🚨 錯誤通知信發信失敗：{Message}", ex.Message);
            context.JournalData.Add("Error", ex.Message);
            context.SetResult(false);
        }
    }
}
