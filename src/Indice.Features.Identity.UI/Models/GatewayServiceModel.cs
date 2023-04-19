﻿namespace Indice.Features.Identity.UI.Models;

/// <summary>Describes services displayed in the home page.</summary>
public class GatewayServiceModel
{
    /// <summary>Link.</summary>
    public string Link { get; set; }
    /// <summary>Display name</summary>
    public string DisplayName { get; set; }
    /// <summary>Image source.</summary>
    public string ImageSrc { get; set; }
    /// <summary>Determines whether is visible.</summary>
    public bool Visible { get; set; }
}
