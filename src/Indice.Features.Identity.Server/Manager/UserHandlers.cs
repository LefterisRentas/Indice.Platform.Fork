﻿using System.Security.Claims;
using IdentityModel;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Indice.Features.Identity.Core;
using Indice.Features.Identity.Core.Data;
using Indice.Features.Identity.Core.Data.Models;
using Indice.Features.Identity.Core.Events;
using Indice.Features.Identity.Server.Devices.Models;
using Indice.Features.Identity.Server.Manager.Models;
using Indice.Features.Identity.Server.Options;
using Indice.Services;
using Indice.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Indice.Features.Identity.Server.Manager;

internal static class UserHandlers
{
    internal static async Task<Ok<ResultSet<UserInfo>>> GetUsers(
        ExtendedIdentityDbContext<User, Role> dbContext,
        [AsParameters] ListOptions options,
        [AsParameters] UserListFilter filter
    ) {
        var query = dbContext.Users.AsNoTracking();
        if (filter != null) {
            query = query.Where(x => filter.ClaimType == null || x.Claims.Any(x => x.ClaimType == filter.ClaimType && x.ClaimValue == filter.ClaimValue));
        }
        // https://docs.microsoft.com/en-us/ef/core/querying/complex-query-operators
        var usersQuery =
            from user in query
            join fnl in dbContext.UserClaims
                on new { user.Id, ClaimType = JwtClaimTypes.GivenName }
                equals new { Id = fnl.UserId, fnl.ClaimType } into fnLeft
            from fn in fnLeft.DefaultIfEmpty()
            join lnl in dbContext.UserClaims
                on new { user.Id, ClaimType = JwtClaimTypes.FamilyName }
                equals new { Id = lnl.UserId, lnl.ClaimType } into lnLeft
            from ln in lnLeft.DefaultIfEmpty()
            select new UserInfo {
                Id = user.Id,
                FirstName = fn.ClaimValue,
                LastName = ln.ClaimValue,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                UserName = user.UserName,
                CreateDate = user.CreateDate,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                TwoFactorEnabled = user.TwoFactorEnabled,
                Blocked = user.Blocked,
                PasswordExpirationPolicy = user.PasswordExpirationPolicy,
                IsAdmin = user.Admin,
                AccessFailedCount = user.AccessFailedCount,
                LastSignInDate = user.LastSignInDate,
                PasswordExpirationDate = user.PasswordExpirationDate
            };
        if (options?.Search?.Length > 2) {
            var userSearchFilterExpression = await IdentityDbContextOptions.UserSearchFilter(dbContext, options.Search);
            usersQuery = usersQuery.Where(userSearchFilterExpression);
        }
        if (filter?.UserId?.Any() == true) {
            usersQuery = usersQuery.Where(x => filter.UserId.Contains(x.Id));
        }
        return TypedResults.Ok(await usersQuery.ToResultSetAsync(options));
    }

    internal static async Task<Results<Ok<SingleUserInfo>, NotFound>> GetUser(
        ExtendedIdentityDbContext<User, Role> dbContext,
        ExtendedConfigurationDbContext configurationDbContext,
        string userId
    ) {
        var foundUser = await (
            from user in dbContext.Users.AsNoTracking()
            where user.Id == userId
            select new SingleUserInfo {
                Id = userId,
                CreateDate = user.CreateDate,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                TwoFactorEnabled = user.TwoFactorEnabled,
                UserName = user.UserName,
                Blocked = user.Blocked,
                PasswordExpirationPolicy = user.PasswordExpirationPolicy,
                IsAdmin = user.Admin,
                AccessFailedCount = user.AccessFailedCount,
                LastSignInDate = user.LastSignInDate,
                PasswordExpirationDate = user.PasswordExpirationDate,
                Claims = user.Claims.Select(claim => new ClaimInfo {
                    Id = claim.Id,
                    Type = claim.ClaimType,
                    Value = claim.ClaimValue
                })
                .ToList(),
                Roles = dbContext.UserRoles.Where(role => role.UserId == userId).Join(
                    dbContext.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => role.Name
                )
                .Cast<string>().ToList()
            }
        )
        .SingleOrDefaultAsync();
        if (foundUser is null) {
            return TypedResults.NotFound();
        }
        var userClaimIds = foundUser.Claims.Select(claim => claim.Type).Cast<string>().ToList();
        if (userClaimIds.Any()) {
            var claimTypes = await configurationDbContext.ClaimTypes.Where(claim => userClaimIds.Contains(claim.Name)).ToListAsync();
            foreach (var claim in foundUser.Claims) {
                var claimType = claimTypes.FirstOrDefault(x => x.Name == claim.Type);
                if (claimType != null) {
                    claim.DisplayName = claimType.DisplayName;
                }
            }
        }
        return TypedResults.Ok(foundUser);
    }

    internal static async Task<Results<CreatedAtRoute<SingleUserInfo>, ValidationProblem>> CreateUser(
        ExtendedUserManager<User> userManager,
        CreateUserRequest request
    ) {
        var user = new User {
            Id = $"{Guid.NewGuid()}",
            UserName = request.UserName,
            Email = request.Email,
            CreateDate = DateTime.UtcNow,
            PhoneNumber = request.PhoneNumber,
            PasswordExpirationPolicy = request.PasswordExpirationPolicy
        };
        IdentityResult? result = null;
        if (string.IsNullOrEmpty(request.Password)) {
            result = await userManager.CreateAsync(user);
        } else {
            result = await userManager.CreateAsync(user, request.Password, validatePassword: !request.BypassPasswordValidation.GetValueOrDefault());
        }
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        if (request.ChangePasswordAfterFirstSignIn.HasValue && request.ChangePasswordAfterFirstSignIn.Value == true) {
            await userManager.SetPasswordExpiredAsync(user, true);
        }
        var claims = request.Claims?.Count() > 0 ? request.Claims.Select(x => new Claim(x.Type!, x.Value!)).ToList() : new List<Claim>();
        if (!string.IsNullOrEmpty(request.FirstName)) {
            claims.Add(new Claim(JwtClaimTypes.GivenName, request.FirstName));
        }
        if (!string.IsNullOrEmpty(request.LastName)) {
            claims.Add(new Claim(JwtClaimTypes.FamilyName, request.LastName));
        }
        if (claims.Any()) {
            await userManager.AddClaimsAsync(user, claims);
        }
        var response = SingleUserInfo.FromUser(user);
        return TypedResults.CreatedAtRoute(response, nameof(GetUser), new { userId = user.Id });
    }

    internal static async Task<Results<Ok<SingleUserInfo>, NotFound, ValidationProblem>> UpdateUser(
        ExtendedIdentityDbContext<User, Role> dbContext,
        ExtendedUserManager<User> userManager,
        string userId,
        UpdateUserRequest request
    ) {
        var user = await dbContext.Users.Include(x => x.Claims).SingleOrDefaultAsync(x => x.Id == userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        if (userManager.EmailAsUserName && !request.BypassEmailAsUserNamePolicy && request.UserName != request.Email) {
            var errors = ValidationErrors.AddError(nameof(request.UserName), "EmailAsUserName policy is applied to the identity system. Email and UserName properties should have the same value. User is not updated.");
            return TypedResults.ValidationProblem(errors);
        }
        user.UserName = request.UserName;
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.TwoFactorEnabled = request.TwoFactorEnabled;
        user.PasswordExpirationPolicy = request.PasswordExpirationPolicy;
        user.Admin = request.IsAdmin;
        user.EmailConfirmed = request.EmailConfirmed;
        user.PhoneNumberConfirmed = request.PhoneNumberConfirmed;
        foreach (var requiredClaim in request.Claims ?? Enumerable.Empty<BasicClaimInfo>()) {
            var claim = user.Claims.SingleOrDefault(x => x.ClaimType == requiredClaim.Type);
            if (claim != null) {
                claim.ClaimValue = requiredClaim.Value;
            } else {
                user.Claims.Add(new IdentityUserClaim<string> {
                    UserId = userId,
                    ClaimType = requiredClaim.Type,
                    ClaimValue = requiredClaim.Value
                });
            }
        }
        await userManager.UpdateAsync(user, request.BypassEmailAsUserNamePolicy);
        var roles = await dbContext.UserRoles.AsNoTracking().Where(x => x.UserId == userId).Join(
            dbContext.Roles,
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, role) => role.Name
        )
        .ToListAsync();
        return TypedResults.Ok(new SingleUserInfo {
            Id = userId,
            CreateDate = user.CreateDate,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            UserName = user.UserName,
            Blocked = user.Blocked,
            Claims = user.Claims.Select(x => new ClaimInfo {
                Id = x.Id,
                Type = x.ClaimType,
                Value = x.ClaimValue
            })
            .ToList(),
            Roles = roles.Cast<string>().ToList(),
        });
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteUser(
        ExtendedIdentityDbContext<User, Role> dbContext,
        ExtendedUserManager<User> userManager,
        string userId
    ) {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        await userManager.DeleteAsync(user);
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> ResendConfirmationEmail(
        ExtendedIdentityDbContext<User, Role> dbContext,
        ExtendedUserManager<User> userManager,
        IPlatformEventService eventService,
        string userId
    ) {
        var user = await dbContext.Users.Include(x => x.Claims).SingleOrDefaultAsync(x => x.Id == userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        if (await userManager.IsEmailConfirmedAsync(user)) {
            var errors = ValidationErrors.AddError(string.Empty, "User's email is already confirmed.");
            return TypedResults.ValidationProblem(errors, detail: errors.Detail());
        }
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await eventService.Publish(new UserRequestForEmailConfirmationEvent(user, token));
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> AddUserRole(
        ExtendedUserManager<User> userManager,
        RoleManager<Role> roleManager,
        ClaimsPrincipal currentUser,
        IPersistedGrantService persistedGrantService,
        string userId,
        string roleId
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        var role = await roleManager.FindByIdAsync(roleId);
        if (role == null) {
            return TypedResults.NotFound();
        }
        if (await userManager.IsInRoleAsync(user, role.Name!)) {
            var errors = ValidationErrors.AddError(nameof(roleId), $"User {user.Email} is already a member of role {role.Name}.");
            return TypedResults.ValidationProblem(errors, detail: errors.Detail());
        }
        var result = await userManager.AddToRoleAsync(user, role.Name!);
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        if (role.IsManagementRole()) {
            var clientId = currentUser.FindFirst(JwtClaimTypes.ClientId);
            await persistedGrantService.RemoveAllGrantsAsync(userId, clientId?.Value);
        }
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteUserRole(
        ExtendedUserManager<User> userManager,
        RoleManager<Role> roleManager,
        IPersistedGrantService persistedGrantService,
        string userId,
        string roleId
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        var role = await roleManager.FindByIdAsync(roleId);
        if (role == null) {
            return TypedResults.NotFound();
        }
        if (!await userManager.IsInRoleAsync(user, role.Name!)) {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> {
                { $"{nameof(roleId)}", new[] { $"User {user.Email} is not a member of role {role.Name}." } }
            });
        }
        var result = await userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        if (role.IsManagementRole()) {
            await persistedGrantService.RemoveAllGrantsAsync(userId);
        }
        return TypedResults.NoContent();
    }

    internal static async Task<Results<Ok<BasicClaimInfo>, NotFound>> GetUserClaim(
        ExtendedIdentityDbContext<User, Role> dbContext,
        string userId,
        int claimId
    ) {
        var claim = await dbContext.UserClaims.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Id == claimId);
        if (claim == null) {
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(new BasicClaimInfo {
            Type = claim.ClaimType,
            Value = claim.ClaimValue
        });
    }

    internal static async Task<Results<CreatedAtRoute<ClaimInfo>, NotFound>> AddUserClaim(
        ExtendedIdentityDbContext<User, Role> dbContext,
        string userId,
        CreateClaimRequest request
    ) {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        var claimToAdd = new IdentityUserClaim<string> {
            UserId = userId,
            ClaimType = request.Type,
            ClaimValue = request.Value
        };
        dbContext.UserClaims.Add(claimToAdd);
        await dbContext.SaveChangesAsync();
        return TypedResults.CreatedAtRoute(new ClaimInfo {
            Id = claimToAdd.Id,
            Type = claimToAdd.ClaimType,
            Value = claimToAdd.ClaimValue
        }, nameof(GetUserClaim), new { userId, claimId = claimToAdd.Id });
    }

    internal static async Task<Results<Ok<ClaimInfo>, NotFound>> UpdateUserClaim(
        ExtendedIdentityDbContext<User, Role> dbContext,
        string userId,
        int claimId,
        UpdateUserClaimRequest request
    ) {
        var userClaim = await dbContext.UserClaims.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == claimId);
        if (userClaim == null) {
            return TypedResults.NotFound();
        }
        userClaim.ClaimValue = request.ClaimValue;
        await dbContext.SaveChangesAsync();
        return TypedResults.Ok(new ClaimInfo {
            Id = userClaim.Id,
            Type = userClaim.ClaimType,
            Value = request.ClaimValue
        });
    }

    internal static async Task<Results<NoContent, NotFound>> DeleteUserClaim(
        ExtendedIdentityDbContext<User, Role> dbContext,
        int claimId,
        string userId
    ) {
        var userClaim = await dbContext.UserClaims.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == claimId);
        if (userClaim == null) {
            return TypedResults.NotFound();
        }
        dbContext.Remove(userClaim);
        await dbContext.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    internal static async Task<Ok<ResultSet<UserClientInfo>>> GetUserApplications(
        IPersistedGrantService persistedGrantService,
        IClientStore clientStore,
        string userId
    ) {
        var userGrants = await persistedGrantService.GetAllGrantsAsync(userId);
        var clients = new List<UserClientInfo>();
        foreach (var grant in userGrants) {
            var client = await clientStore.FindClientByIdAsync(grant.ClientId);
            if (client != null) {
                clients.Add(new UserClientInfo {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName,
                    ClientUri = client.ClientUri,
                    Description = client.Description,
                    LogoUri = client.LogoUri,
                    RequireConsent = client.RequireConsent,
                    AllowRememberConsent = client.AllowRememberConsent,
                    Enabled = client.Enabled,
                    CreatedAt = grant.CreationTime,
                    ExpiresAt = grant.Expiration,
                    Scopes = grant.Scopes
                });
            }
        }
        return TypedResults.Ok(clients.ToResultSet());
    }

    internal static async Task<Ok<ResultSet<DeviceInfo>>> GetUserDevices(
        ExtendedUserManager<User> userManager,
        string userId
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.Ok(new ResultSet<DeviceInfo>());
        }
        var devices = await userManager.GetDevicesAsync(user);
        var response = devices.Select(device => new DeviceInfo {
            ClientType = device.ClientType,
            Data = device.Data,
            DateCreated = device.DateCreated,
            DeviceId = device.DeviceId,
            IsPushNotificationsEnabled = device.IsPushNotificationsEnabled,
            IsTrusted = device.IsTrusted,
            LastSignInDate = device.LastSignInDate,
            Model = device.Model,
            Name = device.Name,
            OsVersion = device.OsVersion,
            Platform = device.Platform,
            SupportsFingerprintLogin = device.SupportsFingerprintLogin,
            SupportsPinLogin = device.SupportsPinLogin
        })
        .ToResultSet();
        return TypedResults.Ok(response);
    }

    internal static async Task<Ok<ResultSet<UserLoginProviderInfo>>> GetUserExternalLogins(
        ExtendedUserManager<User> userManager,
        string userId
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.Ok(new ResultSet<UserLoginProviderInfo>());
        }
        var externalLogins = await userManager.GetLoginsAsync(user);
        var response = externalLogins.Select(x => new UserLoginProviderInfo {
            Key = x.ProviderKey,
            Name = x.LoginProvider,
            DisplayName = !string.IsNullOrWhiteSpace(x.ProviderDisplayName) ? x.ProviderDisplayName : x.LoginProvider
        }).ToResultSet();
        return TypedResults.Ok(response);
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteUserExternalLogin(
        ExtendedUserManager<User> userManager,
        string userId,
        string provider
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        var externalLogins = await userManager.GetLoginsAsync(user);
        var externalLogin = externalLogins.SingleOrDefault(x => x.LoginProvider == provider);
        if (externalLogin == null) {
            return TypedResults.NotFound();
        }
        var result = await userManager.RemoveLoginAsync(user, externalLogin.LoginProvider, externalLogin.ProviderKey);
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> SetUserBlock(
        ExtendedUserManager<User> userManager,
        IPersistedGrantService persistedGrantService,
        string userId,
        SetUserBlockRequest request
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        user.Blocked = request.Blocked;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        if (request.Blocked) {
            // When blocking a user we need to make sure we also revoke all of his tokens.
            await persistedGrantService.RemoveAllGrantsAsync(userId);
        }
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> UnlockUser(
        ExtendedUserManager<User> userManager,
        string userId
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        result = await userManager.ResetAccessFailedCountAsync(user);
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem>> SetPassword(
        ExtendedUserManager<User> userManager,
        string userId,
        SetPasswordRequest request
    ) {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) {
            return TypedResults.NotFound();
        }
        var result = await userManager.ResetPasswordAsync(user, request.Password, validatePassword: !request.BypassPasswordValidation.GetValueOrDefault());
        if (!result.Succeeded) {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary());
        }
        if (request.ChangePasswordAfterFirstSignIn == true) {
            await userManager.SetPasswordExpiredAsync(user, true);
        }
        return TypedResults.NoContent();
    }
}