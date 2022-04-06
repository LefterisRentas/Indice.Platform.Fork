﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Indice.AspNetCore.Filters;
using Indice.Configuration;
using Indice.Features.Messages.AspNetCore.Extensions;
using Indice.Features.Messages.Core;
using Indice.Features.Messages.Core.Events;
using Indice.Features.Messages.Core.Models;
using Indice.Features.Messages.Core.Models.Requests;
using Indice.Features.Messages.Core.Services.Abstractions;
using Indice.Services;
using Indice.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Indice.Features.Messages.AspNetCore.Controllers
{
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [ApiController]
    [ApiExplorerSettings(GroupName = "messages")]
    [Authorize(AuthenticationSchemes = MessagesApi.AuthenticationScheme, Policy = MessagesApi.Policies.BeCampaignManager)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [Route($"{ApiPrefixes.CampaignManagementEndpoints}/campaigns")]
    internal class CampaignsController : CampaignsControllerBase
    {
        public const string Name = "Campaigns";

        public CampaignsController(
            ICampaignService campaignService,
            IDistributionListService distributionListService,
            IContactService contactService,
            Func<string, IFileService> getFileService,
            Func<string, IEventDispatcher> getEventDispatcher,
            IOptions<GeneralSettings> generalSettings,
            IPlatformEventService eventService
        ) : base(getFileService) {
            CampaignService = campaignService ?? throw new ArgumentNullException(nameof(campaignService));
            DistributionListService = distributionListService ?? throw new ArgumentNullException(nameof(distributionListService));
            ContactService = contactService ?? throw new ArgumentNullException(nameof(contactService));
            EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            GeneralSettings = generalSettings?.Value ?? throw new ArgumentNullException(nameof(generalSettings));
            EventDispatcher = getEventDispatcher(KeyedServiceNames.EventDispatcherServiceKey) ?? throw new ArgumentNullException(nameof(getEventDispatcher));
        }

        public ICampaignService CampaignService { get; }
        public IDistributionListService DistributionListService { get; }
        public IContactService ContactService { get; }
        public IPlatformEventService EventService { get; }
        public GeneralSettings GeneralSettings { get; }
        public IEventDispatcher EventDispatcher { get; }

        /// <summary>
        /// Gets the list of all campaigns using the provided <see cref="ListOptions"/>.
        /// </summary>
        /// <param name="options">List parameters used to navigate through collections. Contains parameters such as sort, search, page number and page size.</param>
        /// <response code="200">OK</response>
        [HttpGet]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ResultSet<Campaign>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCampaigns([FromQuery] ListOptions<CampaignsFilter> options) {
            var campaigns = await CampaignService.GetList(options);
            return Ok(campaigns);
        }

        /// <summary>
        /// Gets a campaign with the specified id.
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        [HttpGet("{campaignId:guid}")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(CampaignDetails), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCampaignById([FromRoute] Guid campaignId) {
            var campaign = await CampaignService.GetById(campaignId);
            if (campaign is null) {
                return NotFound();
            }
            return Ok(campaign);
        }

        /// <summary>
        /// Publishes a campaign.
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <response code="204">No Content</response>
        /// <response code="400">Bad Request</response>
        [HttpPost("{campaignId:guid}/publish")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
        public async Task<IActionResult> PublishCampaign([FromRoute] Guid campaignId) {
            var publishedCampaign = await CampaignService.Publish(campaignId);
            // Dispatch event that the campaign was created.
            await EventDispatcher.RaiseEventAsync(
                payload: CampaignPublishedEvent.FromCampaign(publishedCampaign),
                configure: options => options.WrapInEnvelope(false).WithQueueName(EventNames.CampaignPublished)
            );
            return NoContent();
        }

        /// <summary>
        /// Gets the status of a campaign. 
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        [HttpGet("{campaignId:guid}/status")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCampaignStatus([FromRoute] Guid campaignId) {
            await Task.CompletedTask;
            return Ok();
        }

        /// <summary>
        /// Gets the statistics for a specified campaign.
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        [CacheResourceFilter(Expiration = 5)]
        [HttpGet("{campaignId:guid}/statistics")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(CampaignStatistics), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCampaignStatistics([FromRoute] Guid campaignId) {
            var statistics = await CampaignService.GetStatistics(campaignId);
            if (statistics is null) {
                return NotFound();
            }
            return Ok(statistics);
        }

        /// <summary>
        /// Gets the statistics for a specified campaign in the form of an Excel file.
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        [HttpGet("{campaignId:guid}/statistics/export")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(typeof(IFormFile), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CampaignStatistics>> ExportCampaignStatistics([FromRoute] Guid campaignId) {
            var statistics = await CampaignService.GetStatistics(campaignId);
            if (statistics == null) {
                return NotFound();
            }
            return statistics;
        }

        /// <summary>
        /// Creates a new campaign.
        /// </summary>
        /// <param name="request">Contains info about the campaign to be created.</param>
        /// <response code="201">Created</response>
        /// <response code="400">Bad Request</response>
        [Consumes(MediaTypeNames.Application.Json)]
        [HttpPost]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(Campaign), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request) {
            // TODO: Add the following logic to CampaignManager.
            // Create a distribution list (if not given as input) with the same name as the campaign.
            if (!request.DistributionListId.HasValue) {
                var createdList = await DistributionListService.Create(new CreateDistributionListRequest { Name = request.Title });
                request.DistributionListId = createdList.Id;
            }
            // Create campaign in the store.
            var campaign = await CampaignService.Create(request);
            // Create contacts as part of a bulk insert only using the recipient ids.
            if (request.RecipientIds.Any()) {
                var contacts = new List<CreateContactRequest>();
                contacts.AddRange(request.RecipientIds.Select(id => new CreateContactRequest { 
                    RecipientId = id,
                    DistributionListId = request.DistributionListId
                }));
                await ContactService.CreateMany(contacts);
            }
            if (campaign.Published) {
                // Dispatch event that the campaign was created.
                await EventDispatcher.RaiseEventAsync(
                    payload: CampaignPublishedEvent.FromCampaign(campaign),
                    configure: options => options.WrapInEnvelope(false).WithQueueName(EventNames.CampaignPublished)
                );
            }
            return CreatedAtAction(nameof(GetCampaignById), new { campaignId = campaign.Id }, campaign);
        }

        /// <summary>
        /// Updates an existing campaign.
        /// </summary>
        /// <param name="campaignId">The id of the campaign to update.</param>
        /// <param name="request">Contains info about the campaign to update.</param>
        /// <response code="204">No Content</response>
        /// <response code="400">Bad Request</response>
        [Consumes(MediaTypeNames.Application.Json)]
        [HttpPut("{campaignId}")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateCampaign([FromRoute] Guid campaignId, [FromBody] UpdateCampaignRequest request) {
            await CampaignService.Update(campaignId, request);
            return NoContent();
        }

        /// <summary>
        /// Permanently deletes a campaign.
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <response code="204">No Content</response>
        /// <response code="400">Bad Request</response>
        [HttpDelete("{campaignId}")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteCampaign([FromRoute] Guid campaignId) {
            await CampaignService.Delete(campaignId);
            return NoContent();
        }

        /// <summary>
        /// Uploads an attachment for the specified campaign.
        /// </summary>
        /// <param name="campaignId">The id of the campaign.</param>
        /// <param name="file">Contains the stream of the attachment to be uploaded.</param>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        [AllowedFileSize(6291456)] // 6 MegaBytes
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        [HttpPost("{campaignId}/attachment")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(AttachmentLink), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadCampaignAttachment([FromRoute] Guid campaignId, [FromForm] IFormFile file) {
            if (file == null) {
                ModelState.AddModelError(nameof(file), "File is empty.");
                return BadRequest(new ValidationProblemDetails(ModelState));
            }
            var attachment = new FileAttachment(file.OpenReadStream).PopulateFrom(file);
            var attachmentLink = await CampaignService.CreateAttachment(attachment);
            await CampaignService.AssociateAttachment(campaignId, attachmentLink.Id);
            return Ok(attachmentLink);
        }

        /// <summary>
        /// Gets the attachment associated with a campaign.
        /// </summary>
        /// <param name="fileGuid">Contains the photo's Id.</param>
        /// <param name="format">Contains the format of the uploaded attachment extension.</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("attachments/{fileGuid}.{format}")]
        [Produces(MediaTypeNames.Application.Octet)]
        [ProducesResponseType(typeof(IFormFile), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ResponseCache(Duration = 345600, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v" })]
        public async Task<IActionResult> GetCampaignAttachment([FromRoute] Base64Id fileGuid, [FromRoute] string format) => await GetFile("campaigns", fileGuid, format);
    }
}
