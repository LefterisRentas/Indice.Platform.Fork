﻿using System;
using Indice.AspNetCore.Features.Campaigns.Data.Models;
using Indice.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace Indice.AspNetCore.Features.Campaigns.Data
{
    internal class DbTemplateMap : IEntityTypeConfiguration<DbTemplate>
    {
        public DbTemplateMap(IOptions<CampaignsApiOptions> campaignsApiOptions) {
            CampaignsApiOptions = campaignsApiOptions?.Value ?? throw new ArgumentNullException(nameof(campaignsApiOptions));
        }

        public CampaignsApiOptions CampaignsApiOptions { get; }

        public void Configure(EntityTypeBuilder<DbTemplate> builder) {
            // Configure table name.
            builder.ToTable("Template", CampaignsApiOptions.DatabaseSchema);
            // Configure primary key.
            builder.HasKey(x => x.Id);
            // Configure properties.
            builder.Property(x => x.Name).HasMaxLength(TextSizePresets.M128);
            builder.OwnsOne(x => x.Content, options => {
                options.Property(x => x.Sms).HasMaxLength(TextSizePresets.L1024).HasColumnName("SmsContent");
                options.Property(x => x.PushNotification).HasMaxLength(TextSizePresets.L1024).HasColumnName("PushNotificationContent");
                options.Property(x => x.Email).HasColumnName("EmailContent");
            });
        }
    }
}
