﻿using Indice.Features.Risk.Core.Abstractions;
using Indice.Features.Risk.Core.Data;
using Indice.Features.Risk.Core.Data.Models;
using Indice.Features.Risk.Core.Models;
using Indice.Types;
using Microsoft.EntityFrameworkCore;

namespace Indice.Features.Risk.Core.Stores;

internal class RiskEventStoreEntityFrameworkCore : IRiskEventStore
{
    private readonly RiskDbContext _dbContext;

    public RiskEventStoreEntityFrameworkCore(RiskDbContext dbContext) {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task CreateAsync(RiskEvent @event) {
        _dbContext.RiskEvents.Add(@event);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<RiskEvent>> GetList(string subjectId, string[]? types) {
        var query = _dbContext
            .RiskEvents
            .Where(x => x.SubjectId == subjectId);
        if (types?.Any() == true) {
            query = query.Where(x => types.Contains(x.Type));
        }
        return await query.ToListAsync();
    }

    public async Task<ResultSet<RiskEvent>> GetList(ListOptions<AdminRiskFilter> options) {
        var query = _dbContext.RiskEvents.AsNoTracking().AsQueryable();
        query = ApplyFilter(query, options.Filter.Filter);
        return await query.ToResultSetAsync(options);
    }

    private IQueryable<RiskEvent> ApplyFilter(IQueryable<RiskEvent> query, FilterClause[] filter) {
        var dateFromClause = filter.FirstOrDefault(c => c.Member.ToLower() == "from");
        var dateToClause = filter.FirstOrDefault(c => c.Member.ToLower() == "to");

        if (DateTimeOffset.TryParse(dateFromClause.Value, out var dateFrom) && DateTimeOffset.TryParse(dateToClause.Value, out var dateTo)) {
            if (dateFrom == dateTo) {
                dateTo = dateTo.Date.AddDays(1).AddTicks(-1);
            }

            query = query.Where(c => c.CreatedAt >= dateFrom);
            query = query.Where(c => c.CreatedAt <= dateTo);
        }

        foreach (var clause in filter) {
            if (string.IsNullOrWhiteSpace(clause.Member)) {
                continue;
            }

            if (clause.Member.Equals(nameof(RiskEvent.SubjectId), StringComparison.OrdinalIgnoreCase)) {
                switch (clause.Operator) {
                    case FilterOperator.Eq:
                        query = query.Where(x => x.SubjectId.Equals(clause.Value));
                        break;
                    case FilterOperator.Neq:
                        query = query.Where(x => !x.SubjectId.Equals(clause.Value));
                        break;
                    case FilterOperator.Contains:
                        query = query.Where(x => x.SubjectId.Contains(clause.Value));
                        break;
                }
            }

            if (clause.Member.Equals(nameof(RiskEvent.Name), StringComparison.OrdinalIgnoreCase)) {
                switch (clause.Operator) {
                    case FilterOperator.Eq:
                        query = query.Where(x => !string.IsNullOrEmpty(x.Name) && x.Name.Equals(clause.Value));
                        break;
                    case FilterOperator.Neq:
                        query = query.Where(x => !string.IsNullOrEmpty(x.Name) && !x.Name.Equals(clause.Value));
                        break;
                    case FilterOperator.Contains:
                        query = query.Where(x => !string.IsNullOrEmpty(x.Name) && x.Name.Contains(clause.Value));
                        break;
                }
            }

            if (clause.Member.Equals(nameof(RiskEvent.Type), StringComparison.OrdinalIgnoreCase)) {
                switch (clause.Operator) {
                    case FilterOperator.Eq:
                        query = query.Where(x => x.Type.Equals(clause.Value));
                        break;
                    case FilterOperator.Neq:
                        query = query.Where(x => !x.Type.Equals(clause.Value));
                        break;
                    case FilterOperator.Contains:
                        query = query.Where(x => x.Type.Contains(clause.Value));
                        break;
                }
            }

            if (clause.Member.Equals(nameof(RiskEvent.IpAddress), StringComparison.OrdinalIgnoreCase)) {
                switch (clause.Operator) {
                    case FilterOperator.Eq:
                        query = query.Where(x => !string.IsNullOrEmpty(x.IpAddress) && x.IpAddress.Equals(clause.Value));
                        break;
                    case FilterOperator.Neq:
                        query = query.Where(x => !string.IsNullOrEmpty(x.IpAddress) && !x.IpAddress.Equals(clause.Value));
                        break;
                    case FilterOperator.Contains:
                        query = query.Where(x => !string.IsNullOrEmpty(x.IpAddress) && x.IpAddress.Contains(clause.Value));
                        break;
                }
            }
        }

        return query;
    }
}
