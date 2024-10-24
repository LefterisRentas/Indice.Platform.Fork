﻿using FluentValidation;
using Indice.Features.Risk.Core.Abstractions;
using Indice.Features.Risk.Core.Data;
using Indice.Features.Risk.Core.Models;
using Indice.Features.Risk.Core.Stores;
using Indice.Features.Risk.Core.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Indice.Features.Risk.Core.Configuration;

/// <summary>Builder class used to configure the risk engine feature.</summary>
public class RiskEngineBuilder
{
    private readonly List<string> _ruleNames = new();
    private readonly IServiceCollection _services;

    internal RiskEngineBuilder(IServiceCollection services) {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>Adds a new rule to the risk engine by providing an implementation of <see cref="RiskRule"/>.</summary>
    /// <typeparam name="TRule">The implementation type.</typeparam>
    /// <typeparam name="TOptions">The options associated with the rule.</typeparam>
    /// <typeparam name="TValidator">The validator associated with the rule options.</typeparam>
    /// <param name="name">The name of the rule.</param>
    /// <returns>The instance of <see cref="RiskEngineBuilder"/>.</returns>
    public RiskEngineBuilder AddRule<TRule, TOptions, TValidator>(string name)
        where TRule : RiskRule
        where TOptions : RuleOptions, new()
        where TValidator : RuleOptionsValidator<TOptions>, new() {
        CheckAndAddRuleName(name);
        _services.AddTransient<IValidator<TOptions>, TValidator>();
        _services.AddOptions<TOptions>().BindConfiguration($"{Constants.RuleOptionsSectionName}:{name}");
        _services.AddTransient<RiskRule, TRule>();
        return this;
    }

    /// <summary>Registers an implementation of <see cref="IRiskEventStore"/> where Entity Framework Core is used as a persistent mechanism.</summary>
    /// <param name="dbContextOptionsBuilderAction">The builder being used to configure the context.</param>
    public void AddEntityFrameworkCoreStore(Action<DbContextOptionsBuilder> dbContextOptionsBuilderAction) {
        _services.AddDbContext<RiskDbContext>(dbContextOptionsBuilderAction);
        _services.AddTransient<IRuleOptionsStore, RuleStoreEntityFrameworkCore>();
        _services.AddTransient<IRiskEventStore, RiskEventStoreEntityFrameworkCore>();
        _services.AddTransient<IRiskResultStore, RiskResultStoreEntityFrameworkCore>();
    }

    private void CheckAndAddRuleName(string ruleName) {
        if (_ruleNames.Contains(ruleName, StringComparer.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"A rule with name {ruleName} is already configured in the risk engine.");
        }
        _ruleNames.Add(ruleName);
    }
}
