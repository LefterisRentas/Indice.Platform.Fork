﻿using Indice.AspNetCore.Extensions;
using Indice.Features.Identity.Core.Data.Models;
using Indice.Features.Identity.Core.ImpossibleTravel;
using Indice.Features.Identity.SignInLogs.Abstractions;
using Indice.Features.Identity.SignInLogs.Data;
using Indice.Features.Identity.SignInLogs.Models;
using Indice.Features.Identity.SignInLogs.Services;
using Indice.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Indice.Features.Identity.Server.ImpossibleTravel.Services;

/// <summary>A service that detects whether a login attempt is made from an impossible location.</summary>
/// <typeparam name="TUser"></typeparam>
public class ImpossibleTravelDetector<TUser> : IImpossibleTravelDetector<TUser> where TUser : User
{
    private readonly IPAddressLocator? _ipAddressLocator;
    private readonly ISignInLogStore? _signInLogStore;

    /// <summary></summary>
    /// <param name="options">Configuration options for impossible travel detector feature.</param>
    /// <param name="ipAddressLocator"></param>
    /// <param name="signInLogStore">A service that contains operations used to persist the data of a user's sign in event.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ImpossibleTravelDetector(
        IOptions<ImpossibleTravelDetectorOptions> options,
        IPAddressLocator? ipAddressLocator = null,
        ISignInLogStore? signInLogStore = null) {
        Options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _ipAddressLocator = ipAddressLocator;
        _signInLogStore = signInLogStore;
    }

    /// <inheritdoc />
    public ImpossibleTravelDetectorOptions Options { get; init; }

    /// <inheritdoc />
    public async Task<bool> IsImpossibleTravelLogin(HttpContext httpContext, TUser user) {
        if (_ipAddressLocator is null || _signInLogStore is null || httpContext is null || user is null) {
            return false;
        }
        var previousLogins = (await _signInLogStore.ListAsync(
            new ListOptions {
                Page = 1,
                Size = 2,
                Sort = $"{nameof(DbSignInLogEntry.CreatedAt)}-"
            },
            new SignInLogEntryFilter {
                SignInType = SignInType.Interactive,
                Subject = user.Id,
                To = DateTimeOffset.UtcNow
            }
        ))
        .Items;
        if (previousLogins is null || !previousLogins.Any()) {
            return false;
        }
        var ipAddress = httpContext.GetClientIpAddress();
        if (ipAddress is null) {
            return false;
        }
        var result = false;
        foreach (var previousLogin in previousLogins) {
            var currentLoginCoordinates = _ipAddressLocator.GetLocationMetadata(ipAddress)?.Coordinates;
            var previousLoginCoordinates = previousLogin.Coordinates;
            if (currentLoginCoordinates is null || previousLoginCoordinates is null) {
                continue;
            }
            var distanceBetweenLogins = currentLoginCoordinates.Distance(previousLoginCoordinates);
            var travelSpeed = distanceBetweenLogins / (DateTimeOffset.UtcNow - previousLogin.CreatedAt).TotalHours;
            if (travelSpeed > Options.AcceptableSpeed) {
                result = true;
                break;
            }
        }
        return result;
    }
}