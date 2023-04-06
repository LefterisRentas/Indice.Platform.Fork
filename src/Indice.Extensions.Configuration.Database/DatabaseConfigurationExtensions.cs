﻿using Indice.Extensions.Configuration.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Hosting;

/// <summary>Extension methods for registering <see cref="EntityConfigurationProvider{T}"/> with <see cref="IConfigurationBuilder"/>.</summary>
public static class DatabaseConfigurationExtensions
{
    /// <summary>Registers and configures the <see cref="EntityConfigurationProvider{T}"/> using some default values.</summary>
    /// <param name="webHostBuilder">A builder for <see cref="IWebHost"/>.</param>
    /// <param name="configureAction">The <see cref="EntityConfigurationOptions"/> to use.</param>
    /// <returns>The <see cref="IHostBuilder"/>.</returns>
    public static IWebHostBuilder AddDatabaseSettings<TContext>(this IWebHostBuilder webHostBuilder, Action<EntityConfigurationOptions, IConfiguration> configureAction) where TContext : DbContext, IAppSettingsDbContext =>
        webHostBuilder.ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) => {
            var options = new EntityConfigurationOptions();
            configureAction?.Invoke(options, configurationBuilder.Build());
            var result = options.Validate();
            if (!result.Succedded) {
                throw new ArgumentException(result.Error);
            }
            configurationBuilder.Add(new EntityConfigurationSource<TContext>(options));
        })
        .ConfigureServices((context, services) => {
            services.AddTransient<IAppSettingsDbContext, TContext>();
        });
}
