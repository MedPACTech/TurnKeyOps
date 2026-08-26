using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Configurations;
using Microsoft.Extensions.Caching.Memory;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Lib.Utils;

namespace MedInsights.API.DependencyInjection;

public static class TurnKeyOpsFeatureDependencyInjection
{
    public static IServiceCollection AddTurnKeyOpsFeatureConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WeatherSettings>(configuration.GetSection("WeatherSettings"));
        services.Configure<QuoteRequestTenantOptions>(configuration.GetSection(QuoteRequestTenantOptions.SectionName));
        return services;
    }

    public static IServiceCollection AddTurnKeyOpsCompatibility(this IServiceCollection services)
    {
        services.AddScoped<TurnKeyOps.Lib.Utils.IUserContext>(sp =>
            new UserContextAdapter(sp.GetRequiredService<MedInsights.Lib.Utils.IUserContext>()));
        return services;
    }

    public static IServiceCollection AddTurnKeyOpsFeatureAzureTableMappings(this IServiceCollection services)
    {
        services.AddAzureEntityMapping<Customer>(o =>
        {
            o.TableName = "Customers";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<QuoteRequest>(o =>
        {
            o.TableName = "QuoteRequests";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<QuoteEstimate>(o =>
        {
            o.TableName = "QuoteEstimates";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<JobSite>(o =>
        {
            o.TableName = "JobSites";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<Job>(o =>
        {
            o.TableName = "Jobs";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<CalendarEvent>(o =>
        {
            o.TableName = "CalendarEvents";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<Estimate>(o =>
        {
            o.TableName = "Estimates";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<EstimateDefaultsProfile>(o =>
        {
            o.TableName = "EstimateDefaults";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<EstimateLineItem>(o =>
        {
            o.TableName = "EstimateLineItems";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<EstimateTemplate>(o =>
        {
            o.TableName = "EstimateTemplates";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<Invoice>(o =>
        {
            o.TableName = "Invoices";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        services.AddAzureEntityMapping<InvoiceLineItem>(o =>
        {
            o.TableName = "InvoiceLineItems";
            o.WriteKey = (_, e) => new AzureEntityKey { PartitionKey = e.PartitionKey, RowKey = e.RowKey };
            o.EnableIdLocator = true;
        });

        return services;
    }

    public static IServiceCollection AddTurnKeyOpsFeatureRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<IBaseRepositoryAsync<CalendarEvent>>(sp => sp.GetRequiredService<ICalendarEventRepository>());

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBaseRepositoryAsync<Customer>>(sp => sp.GetRequiredService<ICustomerRepository>());

        services.AddScoped<IQuoteRequestRepository, QuoteRequestRepository>();
        services.AddScoped<IBaseRepositoryAsync<QuoteRequest>>(sp => sp.GetRequiredService<IQuoteRequestRepository>());

        services.AddScoped<IQuoteEstimateRepository, QuoteEstimateRepository>();
        services.AddScoped<IBaseRepositoryAsync<QuoteEstimate>>(sp => sp.GetRequiredService<IQuoteEstimateRepository>());

        services.AddScoped<IEstimateRepository, EstimateRepository>();
        services.AddScoped<IBaseRepositoryAsync<Estimate>>(sp => sp.GetRequiredService<IEstimateRepository>());

        services.AddScoped<IEstimateDefaultsRepository, EstimateDefaultsRepository>();
        services.AddScoped<IBaseRepositoryAsync<EstimateDefaultsProfile>>(sp => sp.GetRequiredService<IEstimateDefaultsRepository>());

        services.AddScoped<IEstimateLineItemRepository, EstimateLineItemRepository>();
        services.AddScoped<IBaseRepositoryAsync<EstimateLineItem>>(sp => sp.GetRequiredService<IEstimateLineItemRepository>());

        services.AddScoped<IEstimateTemplateRepository, EstimateTemplateRepository>();
        services.AddScoped<IBaseRepositoryAsync<EstimateTemplate>>(sp => sp.GetRequiredService<IEstimateTemplateRepository>());

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IBaseRepositoryAsync<Invoice>>(sp => sp.GetRequiredService<IInvoiceRepository>());

        services.AddScoped<IInvoiceLineItemRepository, InvoiceLineItemRepository>();
        services.AddScoped<IBaseRepositoryAsync<InvoiceLineItem>>(sp => sp.GetRequiredService<IInvoiceLineItemRepository>());

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IBaseRepositoryAsync<Job>>(sp => sp.GetRequiredService<IJobRepository>());

        services.AddScoped<IJobSiteRepository, JobSiteRepository>();
        services.AddScoped<IBaseRepositoryAsync<JobSite>>(sp => sp.GetRequiredService<IJobSiteRepository>());

        return services;
    }

    public static IServiceCollection AddTurnKeyOpsFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IQuoteRequestTenantResolver, QuoteRequestTenantResolver>();
        services.AddScoped<IQuoteRequestService, QuoteRequestService>();
        services.AddScoped<IQuoteRequestAttachmentService, QuoteRequestAttachmentService>();
        services.AddScoped<IQuoteEstimateService, QuoteEstimateService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IEstimateWorkflowPayloadStore, EstimateWorkflowPayloadStore>();
        services.AddScoped<IEstimateService, EstimateService>();
        services.AddScoped<IEstimateDefaultsService, EstimateDefaultsService>();
        services.AddScoped<IInvoiceWorkflowPayloadStore, InvoiceWorkflowPayloadStore>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoiceWebhookService, InvoiceWebhookService>();
        services.AddScoped<IJobWorkflowPayloadStore, JobWorkflowPayloadStore>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IJobSiteService, JobSiteService>();
        services.AddScoped<IWeatherService, WeatherService>();
        services.AddScoped<ITurnKeyChatService, TurnKeyChatService>();

        return services;
    }

    public static IServiceCollection AddTurnKeyOpsExternalClients(this IServiceCollection services)
    {
        services.AddHttpClient("WeatherGov", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TurnKeyOps/1.0 (contact@turnkeyops.ai)");
        });

        return services;
    }
}
