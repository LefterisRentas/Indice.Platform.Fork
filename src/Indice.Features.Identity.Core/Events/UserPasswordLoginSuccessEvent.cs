﻿using IdentityServer4.Events;

namespace Indice.Features.Identity.Core.Events;

/// <summary>Event for successful user password authentication.</summary>
public class UserPasswordLoginSuccessEvent : Event
{
    /// <summary>Creates a new instance of the <see cref="UserLoginSuccessEvent"/> class.</summary>
    public UserPasswordLoginSuccessEvent() : base(EventCategories.Authentication, "User Password Login Success", EventTypes.Success, 6000) { }

    /// <summary>Creates a new instance of the <see cref="UserLoginSuccessEvent"/> class.</summary>
    /// <param name="username">The username.</param>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="name">The name.</param>
    /// <param name="interactive">if set to <c>true</c> [interactive].</param>
    /// <param name="clientId">The client id.</param>
    public UserPasswordLoginSuccessEvent(
        string username,
        string subjectId,
        string name,
        bool interactive = true,
        string clientId = null
    ) : this() {
        Username = username;
        SubjectId = subjectId;
        DisplayName = name;
        ClientId = clientId;
        Endpoint = interactive ? "UI" : "Token";
    }

    /// <summary>Gets the username.</summary>
    public string Username { get; set; }
    /// <summary>Gets the subject identifier.</summary>
    public string SubjectId { get; set; }
    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; set; }
    /// <summary>Gets the endpoint.</summary>
    public string Endpoint { get; set; }
    /// <summary>Gets the client id.</summary>
    public string ClientId { get; set; }
}