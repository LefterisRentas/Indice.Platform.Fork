﻿using System.Collections.Generic;
using Indice.Features.Cases.Data.Models;

namespace Indice.Features.Cases.Models.Responses
{
    /// <summary>
    /// Models case details.
    /// </summary>
    public class CaseDetails : CasePartial
    {
        /// <summary>
        /// The attachments of the case.
        /// </summary>
        public List<CaseAttachment> Attachments { get; set; } = new();

        /// <summary>
        /// The back-office users that approved the case.
        /// </summary>
        public IEnumerable<AuditMeta>? Approvers { get; set; }
    }
}
