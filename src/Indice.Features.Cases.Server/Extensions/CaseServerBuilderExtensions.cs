﻿using Elsa.Persistence;
using Elsa.Services;
using Elsa.Services.Bookmarks;
using Indice.Features.Cases;
using Indice.Features.Cases.Data;
using Indice.Features.Cases.Events;
using Indice.Features.Cases.Extensions;
using Indice.Features.Cases.Factories;
using Indice.Features.Cases.Handlers;
using Indice.Features.Cases.Interfaces;
using Indice.Features.Cases.Resources;
using Indice.Features.Cases.Server;
using Indice.Features.Cases.Server.Options;
using Indice.Features.Cases.Services;
using Indice.Features.Cases.Services.CaseMessageService;
using Indice.Features.Cases.Services.NoOpServices;
using Indice.Features.Cases.Workflows.Interfaces;
using Indice.Features.Cases.Workflows.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds all services needed to configure Case management server.
/// </summary>
public static class CaseServerBuilderExtensions
{
    /// <summary>Adds the case server dependencies.</summary>
    /// <param name="services">Specifies the contract for a collection of service descriptors.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    /// <param name="environment">Provides information about the web hosting environment an application is running in.</param>
    /// <param name="setupAction">The setup action.</param>
    /// <returns>The <see cref="ICaseServerBuilder"/>.</returns>
    public static ICaseServerBuilder AddCaseServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        Action<CaseServerOptions>? setupAction = null
    )
    {
        var builder = new CaseServerBuilder(services, configuration, environment);
        return builder.AddCaseServerDefaults(setupAction);
    }

    /// <summary>Add the Backoffice configuration. This includes both apis and services to run backoffice operations</summary>
    /// <param name="builder">The builder</param>
    /// <returns>The builder</returns>
    public static ICaseServerBuilder AddWorkflow(this ICaseServerBuilder builder)
    {
        // Register internal handlers
        //builder.Services.AddCaseEventHandler<CaseSubmittedEvent, StartWorkflowHandler>();
        return builder;
    }

    /// <summary>Add the Backoffice configuration. This includes both apis and services to run backoffice operations</summary>
    /// <param name="builder">The builder</param>
    /// <returns>The builder</returns>
    public static ICaseServerBuilder AddCaseManagerEndpoints(this ICaseServerBuilder builder, Action<AdminCasesApiOptions>? setupAction = null) {

        // Configure options given by the consumer.
        var casesApiOptions = new AdminCasesApiOptions();
        setupAction?.Invoke(casesApiOptions);
        builder.Services.Configure<AdminCasesApiOptions>(options => {
            options.ApiPrefix = casesApiOptions.ApiPrefix;
            options.ConfigureDbContext = casesApiOptions.ConfigureDbContext;
            options.DatabaseSchema = casesApiOptions.DatabaseSchema;
            options.ExpectedScope = casesApiOptions.ExpectedScope;
            PrincipalExtensions.Scope = casesApiOptions.ExpectedScope;
            options.UserClaimType = casesApiOptions.UserClaimType;
            options.GroupIdClaimType = casesApiOptions.GroupIdClaimType;
            options.GroupName = casesApiOptions.GroupName;
            options.PermittedAttachmentFileExtensions = casesApiOptions.PermittedAttachmentFileExtensions ?? options.PermittedAttachmentFileExtensions;
        }).AddSingleton(casesApiOptions);

        builder.Services.TryAddTransient<IAdminCaseService, AdminCaseService>();
        builder.Services.AddTransient<IAdminReportService, AdminReportService>();
        builder.Services.AddTransient<IQueryService, QueryService>();
        builder.Services.AddTransient<ICaseAuthorizationService, MemberAuthorizationService>();
        //TODO: Add missing dependecies!


        //builder.Services.AddTransient<ICaseApprovalService, CaseApprovalService>();
        //builder.Services.AddTransient<ICaseActionsService, CaseActionsService>();
        builder.Services.AddTransient<IAdminCaseMessageService, AdminCaseMessageService>();
        builder.Services.AddTransient<ISchemaValidator, SchemaValidator>();
        builder.Services.AddTransient<INotificationSubscriptionService, NotificationSubscriptionService>();
        

        /*builder.Services.AddSmsServiceYubotoOmni(builder.Configuration)
    .AddViberServiceYubotoOmni(builder.Configuration)
    .AddEmailServiceSparkPost(builder.Configuration)
    .WithMvcRazorRendering();*/
        
        builder.Services.AddTransient<CasesMessageDescriber>();
        builder.Services.AddTransient<IJsonTranslationService, JsonTranslationService>();
        builder.Services.AddSingleton<CaseSharedResourceService>(); // Add the service even if there is no resx file, so the runtime will not throw exception

        //add the provider that filters through all available ICaseAuthorizationServices
        builder.Services.AddTransient<ICaseAuthorizationProvider, AggregateCaseAuthorizationProvider>();

        return builder;
    }


    /// <summary>My cases endpoints. This includes customer facing apis and services</summary>
    /// <param name="builder">The builder</param>
    /// <param name="setupAction"></param>
    /// <returns>The builder</returns>
    public static ICaseServerBuilder AddMyCasesEndpoints(this ICaseServerBuilder builder, Action<MyCasesApiOptions>? setupAction = null)
    {
        // Configure options given by the consumer.
        /*
         var casesApiOptions = new CaseServerEndpointOptions();
        setupAction?.Invoke(casesApiOptions);
        builder.Services.Configure<CaseServerEndpointOptions>(options => {
            options.ApiPrefix = casesApiOptions.ApiPrefix;
            options.DatabaseSchema = casesApiOptions.DatabaseSchema;
            options.ApiScope = casesApiOptions.ApiScope;
            options.UserClaimType = casesApiOptions.UserClaimType;
            options.GroupIdClaimType = casesApiOptions.GroupIdClaimType;
            options.GroupName = casesApiOptions.GroupName;
        });
        */
        // Configure options given by the consumer.
        var casesApiOptions = new MyCasesApiOptions();
        setupAction?.Invoke(casesApiOptions);
        builder.Services.Configure<MyCasesApiOptions>(options => {
            options.ApiPrefix = casesApiOptions.ApiPrefix;
            options.ConfigureDbContext = casesApiOptions.ConfigureDbContext;
            options.DatabaseSchema = casesApiOptions.DatabaseSchema;
            options.ExpectedScope = casesApiOptions.ExpectedScope;
            options.UserClaimType = casesApiOptions.UserClaimType;
            options.GroupIdClaimType = casesApiOptions.GroupIdClaimType;
            options.GroupName = casesApiOptions.GroupName;
            options.PermittedAttachmentFileExtensions = casesApiOptions.PermittedAttachmentFileExtensions ?? options.PermittedAttachmentFileExtensions;
        }).AddSingleton(casesApiOptions);

        return builder;
    }

    private static ICaseServerBuilder AddCaseServerDefaults(this ICaseServerBuilder builder, Action<CaseServerOptions>? setupAction = null) {
        // Configure options given by the consumer.
        var serverOptions = new CaseServerOptions();
        setupAction?.Invoke(serverOptions);
        builder.Services.Configure<CaseServerOptions>(options => {
            options.ConfigureDbContext = serverOptions.ConfigureDbContext;
        });
        // Try add general settings.
        builder.Services.AddGeneralSettings(builder.Configuration);
        // Register framework services.
        builder.Services.AddHttpContextAccessor();

        // Register no op services.
        builder.Services.AddLookupService<NoOpLookupService>(nameof(NoOpLookupService)); // needed for factory instantiation
        builder.Services.TryAddTransient<ICustomerIntegrationService, NoOpCustomerIntegrationService>();
        builder.Services.TryAddTransient<ICasePdfService, NoOpCasePdfService>();

        // Register LookupService Factory
        builder.Services.TryAddTransient<ILookupServiceFactory, DefaultLookupServiceFactory>();

        // Register custom services.

        builder.Services.TryAddTransient<IMyCaseService, MyCaseService>();
        builder.Services.TryAddTransient<ICaseTypeService, CaseTypeService>();
        builder.Services.TryAddTransient<ISchemaValidator, SchemaValidator>();
        builder.Services.TryAddTransient<ICheckpointTypeService, CheckpointTypeService>();
        //builder.Services.TryAddTransient<ICaseTemplateService, CaseTemplateService>();
        builder.Services.TryAddTransient<IMyCaseMessageService, MyCaseMessageService>();
        builder.Services.TryAddTransient<IJsonTranslationService, JsonTranslationService>();
        builder.Services.TryAddSingleton<CaseSharedResourceService>(); // Add the service even if there is no resx file, so the runtime will not throw exception
       


        // Register events.
        builder.Services.AddTransient<ICaseEventService, CaseEventService>();

        // Register application DbContext.
        builder.Services.AddDbContext<CasesDbContext>(serverOptions.ConfigureDbContext ?? (sqlBuilder => sqlBuilder.UseSqlServer(builder.Configuration.GetConnectionString("StorageConnection"))));

        return builder;

    }
}

