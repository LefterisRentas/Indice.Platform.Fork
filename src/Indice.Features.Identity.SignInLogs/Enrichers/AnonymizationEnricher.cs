﻿using System.Net;
using Indice.Features.Identity.SignInLogs.Abstractions;
using Indice.Features.Identity.SignInLogs.Models;
using Microsoft.Extensions.Options;

namespace Indice.Features.Identity.SignInLogs.Enrichers;

internal class AnonymizationEnricher : ISignInLogEntryEnricher
{
    private readonly SignInLogOptions _signInLogOptions;

    public AnonymizationEnricher(IOptions<SignInLogOptions> signInLogOptions) {
        _signInLogOptions = signInLogOptions?.Value ?? throw new ArgumentNullException(nameof(signInLogOptions));
    }

    public int Priority => int.MaxValue;
    public EnricherDependencyType DependencyType => EnricherDependencyType.Default;

    public ValueTask EnrichAsync(SignInLogEntry logEntry) {
        if (!_signInLogOptions.AnonymizePersonalData) {
            return ValueTask.CompletedTask;
        }
        logEntry.IpAddress = IPAddress.Any.ToString();
        return ValueTask.CompletedTask;
    }
}
