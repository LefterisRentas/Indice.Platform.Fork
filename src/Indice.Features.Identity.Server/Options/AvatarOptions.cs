﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Indice.Features.Identity.Server.Options;

/// <summary>Options for the avatar management.</summary>
public class AvatarOptions
{
    private readonly HashSet<int> _allowedSizes = new HashSet<int>() { 24, 32, 64, 128 };
    /// <summary>Controls whether the avatar management feature is enabled. Defaults to false.</summary>
    public bool IsAvatarEnabled { get; set; } = false;
    /// <summary>The maximum acceptable size of the files to be uploaded in bytes. Defaults to <i>100KB</i>.</summary>
    public long MaxFileSize { get; set; } = 100 * 1024;;
    /// <summary>The acceptable file extensions. Defaults to <i>.png, .jpg, .gif</i>.</summary>
    public string AcceptableFileExtensions { get; set; } = ".png, .jpg, .gif";
    /// <summary>Allowed tile sizes. Only these sizes are available.</summary>
    public ICollection<int> AllowedSizes => _allowedSizes;
}