using Azure.Messaging.ServiceBus;
using IBeam.Communications.Abstractions;
using IBeam.Communications.Email.AzureCommunications;
using IBeam.Communications.Sms.AzureCommunications;
using IBeam.Identity.Api.DependencyInjection;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.API.Infrastructure;
using MedInsights.API.Middleware;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using MedInsights.Services.BackgroundServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;
using MedInsights.API.Configurations;
using MedInsights.API.DependencyInjection;
using IBeam.Identity.Interfaces;
using MedInsights.Services.Events;
using IBeam.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

public partial class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            ProductionIdentityConfiguration.Validate(
                builder.Configuration,
                builder.Environment.EnvironmentName);
            ProductionIntegrationConfiguration.Validate(
                builder.Configuration,
                builder.Environment.EnvironmentName);

            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Local"))
            {
                var repoLocalSecretsPath = Path.GetFullPath(
                    Path.Combine(builder.Environment.ContentRootPath, "..", ".local", "user-secrets.json"));

                builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
                builder.Configuration.AddJsonFile(
                    repoLocalSecretsPath,
                    optional: true,
                    reloadOnChange: true);
            }

            var serviceBusConnection = builder.Configuration.GetConnectionString("AzureServiceBus");
            var hasServiceBusConnection = !string.IsNullOrWhiteSpace(serviceBusConnection);

            // ----- Config & registrations -----
            builder.Services.AddAppConfigurations(builder.Configuration);
            builder.Services.AddTurnKeyOpsFeatureConfigurations(builder.Configuration);
            builder.Services.AddRolePermissionAuthorization(builder.Configuration);
            builder.Services.ConfigureIBeamAzureTables(builder.Configuration);
            builder.Services.AddIBeamAzureTablesRepositories();


            // builder.Services.ConfigureIBeamAzureTables(options =>
            // {
            //     options.ConnectionString = builder.Configuration["AzureStorageSettings:ConnectionString"]
            //         ?? throw new InvalidOperationException("Missing AzureStorageSettings:ConnectionString");
            //     options.TableNamePrefix = string.Empty;
            //     options.CreateTablesIfNotExists = true;
            // });

            builder.Services.AddAzureTableMappings();
            builder.Services.AddTurnKeyOpsFeatureAzureTableMappings();

            builder.Services.AddHostedService<GlobalErrorHandler>();
            builder.Services.AddExternalClients(builder.Configuration);
            builder.Services.AddTurnKeyOpsExternalClients();
            builder.Services.AddRepositories();
            builder.Services.AddTurnKeyOpsFeatureRepositories();
            builder.Services.AddManagedServices(builder.Configuration, enableServiceBus: hasServiceBusConnection);
            builder.Services.AddTurnKeyOpsFeatureServices();
            builder.Services.AddIBeamCommunications(builder.Configuration);

            // 1) Email provider (Azure Communication Services Email)
            builder.Services.AddIBeamAzureCommunicationsEmail(builder.Configuration);
            builder.Services.AddIBeamCommunicationsSmsAzure(builder.Configuration);

            // IBeam Identity API: wires auth/JWT + identity services from IBeam:* configuration.
            builder.Services.AddIBeamIdentityApi(builder.Configuration);
            builder.Services.PostConfigureAll<JwtBearerOptions>(options =>
                JwtValidationHardening.Apply(options, builder.Configuration));
            builder.Services.AddOtpCompleteRetryDecorator();
            builder.Services.AddScoped<IAuthLifecycleHook, UserProfileHook>();
            builder.Services.AddIBeamIdentityApiControllers();

            builder.Services.AddJwtDebugging();
            
            var allowedClientOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("TurnKeyOpsClients",
                    p => p.WithOrigins(allowedClientOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            // Ensure Dev SQLite folder exists and use a stable absolute path for the DB file
            //var dataDir = Path.Combine(builder.Environment.ContentRootPath, ".data");
            //Directory.CreateDirectory(dataDir); // create if missing
            //// Optional: expose to connection strings using |DataDirectory|
            //AppDomain.CurrentDomain.SetData("DataDirectory", dataDir);

            //JSON enum as strings
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
                });

            // Azure Service Bus
            if (hasServiceBusConnection)
            {
                builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));
            }

            // App services
            if (hasServiceBusConnection)
            {
                builder.Services.AddScoped<ITokenLedgerService, TokenLedgerService>();
                builder.Services.AddHostedService<TokenTransactionWorker>();
            }
            else
            {
                builder.Services.AddScoped<ITokenLedgerService, DisabledTokenLedgerService>();
            }

            builder.Services.AddScoped<IUserProfileService, UserProfileService>();

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "TurnKeyOps API",
                    Version = "v1",
                    Description = "TurnKeyOps backend API"
                });
                var bearer = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste JWT here (no 'Bearer ' prefix)"
                };
                c.AddSecurityDefinition("Bearer", bearer);
                c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer", doc, null),
                        new List<string>()
                    }
                });
                // c.OperationFilter<BinaryResponseOperationFilter>();
            });

            // Data Protection keys in Identity DB
            //builder.Services.AddDataProtection()
            //    .PersistKeysToDbContext<AppIdentityDbContext>();

            // Distributed cache: memory in Dev; SQL Server in non-Dev (avoids failing when SQL isn't available locally)
            // if (builder.Environment.IsDevelopment())
            // {
            //     builder.Services.AddDistributedMemoryCache();
            // }
            // else
            // {
            //     builder.Services.AddDistributedSqlServerCache(options =>
            //     {
            //         options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            //         options.SchemaName = "dbo";
            //         options.TableName = "TokenCache";
            //     });
            // }

            builder.Services.AddMemoryCache();

            // Needed for CurrentUserContext.FromHttp(...)
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TurnKeyAuthorizationResultHandler>();

            // IBeam repository tenant context (safe for both HTTP requests and startup validation).
            builder.Services.AddScoped<ITenantContext>(sp =>
            {
                var http = sp.GetRequiredService<IHttpContextAccessor>();
                var principal = http.HttpContext?.User;

                if (principal?.Identity?.IsAuthenticated != true)
                    return new TenantContext();

                var tenantClaim =
                    principal.FindFirst("tenant_id")?.Value ??
                    principal.FindFirst("tenant")?.Value ??
                    principal.FindFirst("tid")?.Value ??
                    principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

                return Guid.TryParse(tenantClaim, out var tenantId) && tenantId != Guid.Empty
                    ? new TenantContext(tenantId)
                    : new TenantContext();
            });

            // Per-request snapshot of the current user. Background services have no HttpContext,
            // so they must receive an anonymous context instead of throwing during scope creation.
            builder.Services.AddScoped<IUserContext>(sp =>
            {
                var http = sp.GetRequiredService<IHttpContextAccessor>();
                return http.HttpContext is null
                    ? UserContext.Anonymous()
                    : UserContext.FromHttp(http);
            });
            builder.Services.AddTurnKeyOpsCompatibility();

            builder.Services.Configure<AzureSpeechSettings>(
                builder.Configuration.GetSection("AzureSpeechSettings"));

            // ---- Internal API HTTP client ----
            builder.Services.AddHttpClient("MedInsightsApi", c =>
            {
                var baseUrl = builder.Configuration["SystemSettings:APIHost"]
                              ?? throw new InvalidOperationException("Missing SystemSettings:APIHost");
                c.BaseAddress = new Uri(baseUrl);
            });

            var app = builder.Build();

            app.MapGet("/", () => "OK").AllowAnonymous();

            // ---- Auto-migrate + seed (DEV only) ----
            if (app.Environment.IsDevelopment())
            {
               // using var scope = app.Services.CreateScope();
               // var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
               // await db.Database.EnsureDeletedAsync(); // this line clears the DB on each run in Dev
               // await db.Database.MigrateAsync();
               // await IdentitySeeder.SeedAsync(scope.ServiceProvider);
            }

            using (var scope = app.Services.CreateScope())
            {
                var startupSeeder = scope.ServiceProvider.GetRequiredService<IStartupSeeder>();
                await startupSeeder.SeedAsync();
            }

            // Pipeline
            var swaggerEnabled = app.Environment.IsDevelopment()
                                 || app.Environment.IsEnvironment("Local")
                                 || app.Environment.IsEnvironment("Test")
                                 || builder.Configuration.GetValue<bool>("Swagger:Enabled");

            if (swaggerEnabled)
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Local"))
                app.UseHsts();

            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers.Append("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
                await next();
            });
            app.UseHttpsRedirection();
            app.UseCors("TurnKeyOpsClients");
            app.UseAuthentication();
            app.UseAuthorization();

            // Custom exception middleware
            app.UseMiddleware<ApiExceptionMiddleware>();

        

            app.MapControllers();
            app.Run();
        }
        catch (OptionsValidationException ex)
        {
            Console.WriteLine("OPTIONS TYPE FAILED: " + ex.OptionsType.FullName);
            Console.WriteLine("OPTIONS NAME FAILED: " + ex.OptionsName);
            Console.WriteLine("FAILURES: " + string.Join(" | ", ex.Failures));
            throw;
        }
        catch (Exception ex)
        {



            Console.Error.WriteLine($"Startup failure: {ex}");

            // Fallback: log to Azure Table storage (as in your original)
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var settings = config.GetSection("AzureStorageSettings").Get<AzureStorageSettings>();
            if (settings == null)
            {
                Console.Error.WriteLine("AzureStorageSettings section missing in configuration.");
                throw;
            }

            var repo = new SystemErrorRepository(Options.Create(settings));
            repo.SaveAsync(new SystemError
            {
                PartitionKey = DateTime.UtcNow.ToString("yyyyMMdd"),
                RowKey = Guid.NewGuid().ToString(),
                Path = "StartupException",
                Method = "N/A",
                Message = ex.Message,
                StackTrace = ex.ToString(),
                TraceId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow
            }).GetAwaiter().GetResult();

            throw;
        }
    }

}
