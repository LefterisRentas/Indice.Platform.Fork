﻿using Indice.Features.Risk.Core.Abstractions;
using Indice.Features.Risk.Core.Configuration;
using Indice.Features.Risk.Core.Data.Models;
using Microsoft.Extensions.Options;

namespace Indice.Features.Risk.Core.Services;

/// <summary>Manages transactions and events for the risk engine.</summary>
/// <typeparam name="TRiskEvent">The type of transaction.</typeparam>
public class RiskManager<TRiskEvent> where TRiskEvent : DbRiskEvent
{
    private readonly IRiskEventStore<TRiskEvent> _eventStore;

    /// <summary>Creates a new instance of <see cref="RiskManager{TRiskEvent}"/>.</summary>
    /// <param name="rules">Collection of rules registered in the engine.</param>
    /// <param name="rulesConfiguration">Collection of rule configuration.</param>
    /// <param name="eventStore">Store for risk engine events.</param>
    /// <param name="riskEngineOptions">Options used to configure the core risk engine.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public RiskManager(
        IEnumerable<RiskRuleBase<TRiskEvent>> rules,
        IEnumerable<RiskEventModel> rulesConfiguration,
        IRiskEventStore<TRiskEvent> eventStore,
        IOptions<RiskEngineOptions> riskEngineOptions
    ) {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        EventsConfiguration = rulesConfiguration ?? throw new ArgumentNullException(nameof(rulesConfiguration));
        RiskEngineOptions = riskEngineOptions.Value ?? throw new ArgumentNullException(nameof(riskEngineOptions));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <summary>The collection of rules registered in the risk engine.</summary>
    public IEnumerable<RiskRuleBase<TRiskEvent>> Rules { get; }
    /// <summary>The collection of rule configuration provided in the risk engine.</summary>
    public IEnumerable<RiskEventModel> EventsConfiguration { get; }
    /// <summary>Options used to configure the core risk engine.</summary>
    public RiskEngineOptions RiskEngineOptions { get; }

    /// <summary>Gets the risk score for a given event.</summary>
    /// <param name="event">The event occurred for which to calculate the risk score.</param>
    public async Task<AggregateRuleExecutionResult> GetRiskAsync(TRiskEvent @event) {
        var results = new List<RuleExecutionResult>();
        foreach (var rule in Rules) {
            var result = await rule.ExecuteAsync(@event);
            result.RuleName = rule.Name;
            var configuredEvents = EventsConfiguration;
            var finalRiskScore = result.RiskScore;
            result.RiskScore = finalRiskScore;
            result.RiskLevel = RiskEngineOptions.RiskLevelRangeMapping.GetRiskLevel(result.RiskScore) ?? RiskLevel.None;
            results.Add(result);
        }
        return new AggregateRuleExecutionResult(@event.Id, Rules.Count(), results);
    }
}