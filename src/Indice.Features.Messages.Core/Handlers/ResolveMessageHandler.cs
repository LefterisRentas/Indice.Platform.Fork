﻿using System.Dynamic;
using System.Text.Json;
using HandlebarsDotNet;
using Indice.Features.Messages.Core.Events;
using Indice.Features.Messages.Core.Models;
using Indice.Features.Messages.Core.Models.Requests;
using Indice.Features.Messages.Core.Services.Abstractions;
using Indice.Serialization;
using Indice.Services;

namespace Indice.Features.Messages.Core.Handlers
{
    /// <summary>
    /// Job handler for <see cref="ResolveMessageEvent"/>.
    /// </summary>
    public class ResolveMessageHandler : ICampaignJobHandler<ResolveMessageEvent>
    {
        /// <summary>
        /// Creates a new instance of <see cref="ResolveMessageHandler"/>.
        /// </summary>
        /// <param name="getEventDispatcher">Provides methods that allow application components to communicate with each other by dispatching events.</param>
        /// <param name="contactResolver">Contains information that help gather contact information from other systems.</param>
        /// <param name="contactService">A service that contains contact related operations.</param>
        /// <param name="messageService">A service that contains message related operations.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ResolveMessageHandler(
            Func<string, IEventDispatcher> getEventDispatcher,
            IContactResolver contactResolver,
            IContactService contactService,
            IMessageService messageService
        ) {
            GetEventDispatcher = getEventDispatcher ?? throw new ArgumentNullException(nameof(getEventDispatcher));
            ContactResolver = contactResolver ?? throw new ArgumentNullException(nameof(contactResolver));
            ContactService = contactService ?? throw new ArgumentNullException(nameof(contactService));
            MessageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        private Func<string, IEventDispatcher> GetEventDispatcher { get; }
        private IContactResolver ContactResolver { get; }
        private IContactService ContactService { get; }
        private IMessageService MessageService { get; }

        /// <summary>
        /// Decides whether to insert or update a resolved contact.
        /// </summary>
        /// <param name="event">The event model used when a contact is resolved from an external system.</param>
        public async Task Process(ResolveMessageEvent @event) {
            var recipientId = @event.Contact.RecipientId;
            var contactId = @event.Contact.Id;
            var needsUpdate = false;
            var campaign = @event.Campaign;
            Contact contact = null;
            if (!string.IsNullOrWhiteSpace(recipientId)) {
                contact = await ContactService.GetByRecipientId(recipientId);
                if (contact is null) {
                    contact = await ContactResolver.GetById(recipientId);
                }
            } else if (contactId.HasValue) {
                contact = await ContactService.GetById(contactId.Value);
            }
            if (contact is not null && campaign.IsNewDistributionList) {
                await ContactService.AddToDistributionList(campaign.DistributionListId.Value, Mapper.ToCreateDistributionListContactRequest(contact));
            }
            needsUpdate = contact?.UpdatedAt.HasValue == true && (DateTimeOffset.UtcNow - contact.UpdatedAt.Value) > TimeSpan.FromDays(5);
            if (needsUpdate) {
                await ContactService.Update(contact.Id.Value, Mapper.ToUpdateContactRequest(contact, campaign.DistributionListId));
            }
            if (contact is null) {
                contact = @event.Contact;
            }
            // Make substitution to message content using contact resolved data.
            var handlebars = Handlebars.Create();
            handlebars.Configuration.TextEncoder = new HtmlEncoder();
            foreach (var content in campaign.Content) {
                dynamic templateData = new {
                    contact = JsonSerializer.Deserialize<ExpandoObject>(JsonSerializer.Serialize(contact, JsonSerializerOptionDefaults.GetDefaultSettings()), JsonSerializerOptionDefaults.GetDefaultSettings()),
                    data = JsonSerializer.Deserialize<ExpandoObject>(JsonSerializer.Serialize(campaign.Data, JsonSerializerOptionDefaults.GetDefaultSettings()), JsonSerializerOptionDefaults.GetDefaultSettings())
                };
                var messageContent = campaign.Content[content.Key];
                messageContent.Title = handlebars.Compile(content.Value.Title)(templateData);
                messageContent.Body = handlebars.Compile(content.Value.Body)(templateData);
            }
            // Persist message with merged contents.
            await MessageService.Create(new CreateMessageRequest {
                CampaignId = campaign.Id,
                ContactId = contact.Id,
                Content = campaign.Content,
                RecipientId = contact.RecipientId
            });
            var eventDispatcher = GetEventDispatcher(KeyedServiceNames.EventDispatcherServiceKey);
            if (campaign.MessageChannelKind.HasFlag(MessageChannelKind.PushNotification)) {
                await eventDispatcher.RaiseEventAsync(SendPushNotificationEvent.FromContactResolutionEvent(@event, broadcast: false),
                    options => options.WrapInEnvelope(false).At(campaign.ActivePeriod?.From?.DateTime ?? DateTime.UtcNow).WithQueueName(EventNames.SendPushNotification));
            }
            if (campaign.MessageChannelKind.HasFlag(MessageChannelKind.Email)) {
                await eventDispatcher.RaiseEventAsync(SendEmailEvent.FromContactResolutionEvent(@event, broadcast: false),
                    options => options.WrapInEnvelope(false).At(campaign.ActivePeriod?.From?.DateTime ?? DateTime.UtcNow).WithQueueName(EventNames.SendEmail));
            }
            if (campaign.MessageChannelKind.HasFlag(MessageChannelKind.SMS)) {
                await eventDispatcher.RaiseEventAsync(SendSmsEvent.FromContactResolutionEvent(@event, broadcast: false),
                    options => options.WrapInEnvelope(false).At(campaign.ActivePeriod?.From?.DateTime ?? DateTime.UtcNow).WithQueueName(EventNames.SendSms));
            }
        }
    }
}
