﻿using Indice.Features.Risk.Core.Data.Models;

namespace Indice.Features.Risk.Core.Abstractions;

/// <summary>Store for risk engine results.</summary>
public interface IRiskResultStore
{
    /// <summary>Persists a new risk result in the store.</summary>
    /// <param name="riskResult">The calculated risk result.</param>
    Task CreateAsync(DbAggregateRuleExecutionResult riskResult);

    /// <summary>Adds an event Id to risk result.</summary>
    /// <param name="resultId">The Id of the risk result.</param>
    /// <param name="eventId">The Id of the risk event.</param>
    Task AddEventIdAsync(Guid resultId, Guid eventId);
}
