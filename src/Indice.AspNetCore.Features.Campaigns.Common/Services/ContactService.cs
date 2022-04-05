﻿using Indice.AspNetCore.Features.Campaigns.Data;
using Indice.AspNetCore.Features.Campaigns.Data.Models;
using Indice.AspNetCore.Features.Campaigns.Exceptions;
using Indice.AspNetCore.Features.Campaigns.Models;
using Indice.Types;
using Microsoft.EntityFrameworkCore;

namespace Indice.AspNetCore.Features.Campaigns.Services
{
    /// <summary>
    /// An implementation of <see cref="IContactService"/> for Entity Framework Core.
    /// </summary>
    public class ContactService : IContactService
    {
        /// <summary>
        /// Creates a new instance of <see cref="ContactService"/>.
        /// </summary>
        /// <param name="dbContext">The <see cref="Microsoft.EntityFrameworkCore.DbContext"/> for Campaigns API feature.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ContactService(CampaignsDbContext dbContext) {
            DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        private CampaignsDbContext DbContext { get; }

        /// <inheritdoc />
        public async Task AddToDistributionList(Guid id, CreateDistributionListContactRequest request) {
            DbContact contact;
            var list = await DbContext.DistributionLists.FindAsync(id);
            if (list is null) {
                throw CampaignException.DistributionListNotFound(id);
            }
            if (request.Id.HasValue) {
                contact = await DbContext.Contacts.FindAsync(request.Id.Value);
                if (contact is null) {
                    throw CampaignException.ContactNotFound(id);
                }
                contact.DistributionListId = list.Id;
                contact.Email = request.Email;
                contact.FirstName = request.FirstName;
                contact.FullName = request.FullName;
                contact.Id = request.Id ?? Guid.NewGuid();
                contact.LastName = request.LastName;
                contact.PhoneNumber = request.PhoneNumber;
                contact.RecipientId = request.RecipientId;
                contact.Salutation = request.Salutation;
                contact.UpdatedAt = DateTimeOffset.UtcNow;
                await DbContext.SaveChangesAsync();
                return;
            }
            contact = Mapper.ToDbContact(request);
            contact.DistributionListId = id;
            DbContext.Contacts.Add(contact);
            await DbContext.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<Contact> Create(CreateContactRequest request) {
            var contact = Mapper.ToDbContact(request);
            DbContext.Contacts.Add(contact);
            await DbContext.SaveChangesAsync();
            return Mapper.ToContact(contact);
        }

        /// <inheritdoc />
        public async Task CreateMany(IEnumerable<CreateContactRequest> contacts) {
            DbContext.Contacts.AddRange(contacts.Select(contact => Mapper.ToDbContact(contact)));
            await DbContext.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<Contact> GetById(Guid id) {
            var contact = await DbContext.Contacts.FindAsync(id);
            if (contact is null) {
                return default;
            }
            return Mapper.ToContact(contact);
        }

        /// <inheritdoc />
        public async Task<Contact> GetByRecipientId(string id) {
            var contact = await DbContext.Contacts.SingleOrDefaultAsync(x => x.RecipientId == id);
            if (contact is null) {
                return default;
            }
            return Mapper.ToContact(contact);
        }

        /// <inheritdoc />
        public async Task<ResultSet<Contact>> GetList(ListOptions options) {
            var query = DbContext
                .Contacts
                .AsNoTracking()
                .Select(Mapper.ProjectToContact);
            return await query.ToResultSetAsync(options);
        }

        /// <inheritdoc />
        public async Task Update(Guid id, UpdateContactRequest request) {
            var contact = await DbContext.Contacts.FindAsync(id);
            if (contact is null) {
                throw CampaignException.ContactNotFound(id);
            }
            contact.Email = request.Email;
            contact.FirstName = request.FirstName;
            contact.FullName = request.FullName;
            contact.LastName = request.LastName;
            contact.PhoneNumber = request.PhoneNumber;
            contact.Salutation = request.Salutation;
            contact.UpdatedAt = DateTimeOffset.UtcNow;
            await DbContext.SaveChangesAsync();
        }
    }
}
