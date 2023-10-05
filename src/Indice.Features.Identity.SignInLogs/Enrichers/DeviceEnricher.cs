﻿using Indice.Features.Identity.Core.Types;
using Indice.Features.Identity.SignInLogs.Abstractions;
using Indice.Features.Identity.SignInLogs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Indice.Features.Identity.SignInLogs.Enrichers;

/// <summary></summary>
public sealed class DeviceEnricher : ISignInLogEntryEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary></summary>
    /// <param name="httpContextAccessor"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public DeviceEnricher(IHttpContextAccessor httpContextAccessor) {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public int Order => 4;
    /// <inheritdoc />
    public SignInLogEnricherRunType RunType => SignInLogEnricherRunType.Synchronous;

    /// <inheritdoc />
    public ValueTask EnrichAsync(SignInLogEntry logEntry) {
        var userAgentHeader = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];
        if (string.IsNullOrWhiteSpace(userAgentHeader)) {
            return ValueTask.CompletedTask;
        }
        var userAgent = new UserAgent(userAgentHeader);
        logEntry.ExtraData.Device = new SignInLogEntryDevice { 
            Model = userAgent.DeviceModel,
            Platform = userAgent.DevicePlatform,
            UserAgent = userAgent.HeaderValue,
            DisplayName = userAgent.DisplayName,
            Os = userAgent.Os
        };
        return ValueTask.CompletedTask;
    }
}
