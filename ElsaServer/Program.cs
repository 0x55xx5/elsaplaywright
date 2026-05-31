using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Elsa.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Microsoft.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

var services = builder.Services;
var configuration = builder.Configuration;

services
    .AddElsa(elsa => elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions = options => options.SigningKey = "large-signing-key-for-signing-JWT-tokens";
            identity.UseAdminUserProvider();
        })
        .UseDefaultAuthentication()
        .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite("Data Source=elsa.sqlite.db;Cache=Shared")))
        .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite("Data Source=elsa.sqlite.db;Cache=Shared")))
        .UseScheduling()
        .UseJavaScript()
        .UseLiquid()
        .UseCSharp()
        .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
        .UseWorkflowsApi()
        .UseEmail(email => email.ConfigureOptions = options => configuration.GetSection("Smtp").Bind(options))
        .AddActivitiesFrom<Program>()
        .AddWorkflowsFrom<Program>()
    );

services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("*")));
services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));

// 忽略所有 HttpClient 的 SSL 憑證錯誤 (僅限本地測試使用)
builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
{
    options.HttpMessageHandlerBuilderActions.Add(b =>
    {
        b.PrimaryHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseRouting();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseWorkflowsApi();
app.UseWorkflows();
app.MapFallbackToPage("/_Host");

// --- Promotion Validation Dummy APIs ---
// 1. API Validation Endpoint
app.MapPost("/api/cart/calculate", () =>
{
    // 模擬算錢邏輯: 1200 * 0.9 - 100 = 980
    return Results.Ok(new { finalPrice = 980m });
});

// ---------------------------------------
app.Run();