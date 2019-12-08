// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using IdentityServer4.Events;
using IdentityServer4.Extensions;

namespace IdentityServer4.Quickstart.UI
{
	/// <summary>
	/// This sample controller allows a user to revoke grants given to clients
	/// </summary>
	[SecurityHeaders]
	[Authorize]
	public class GrantsController : Controller
	{
		private IIdentityServerInteractionService Interaction { get; }
		private IClientStore Clients { get; }
		private IResourceStore Resources { get; }
		private IEventService Events { get; }

		public GrantsController(IIdentityServerInteractionService interaction,
			IClientStore clients,
			IResourceStore resources,
			IEventService events)
		{
			Interaction = interaction;
			Clients = clients;
			Resources = resources;
			Events = events;
		}

		/// <summary>
		/// Show list of grants
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			return View("Index", await BuildViewModelAsync());
		}

		/// <summary>
		/// Handle postback to revoke a client
		/// </summary>
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Revoke(string clientId)
		{
			await Interaction.RevokeUserConsentAsync(clientId);
			await Events.RaiseAsync(new GrantsRevokedEvent(User.GetSubjectId(), clientId));

			return RedirectToAction("Index");
		}

		private async Task<GrantsViewModel> BuildViewModelAsync()
		{
			var grants = await Interaction.GetAllUserConsentsAsync();

			var list = new List<GrantViewModel>();
			foreach (var grant in grants)
			{
				var client = await Clients.FindClientByIdAsync(grant.ClientId);
				if (client != null)
				{
					var resources = await Resources.FindResourcesByScopeAsync(grant.Scopes);

					var item = new GrantViewModel()
					{
						ClientId = client.ClientId,
						ClientName = client.ClientName ?? client.ClientId,
						ClientLogoUrl = client.LogoUri,
						ClientUrl = client.ClientUri,
						Created = grant.CreationTime,
						Expires = grant.Expiration,
						IdentityGrantNames = resources.IdentityResources.Select(x => x.DisplayName ?? x.Name).ToArray(),
						ApiGrantNames = resources.ApiResources.Select(x => x.DisplayName ?? x.Name).ToArray()
					};

					list.Add(item);
				}
			}

			return new GrantsViewModel
			{
				Grants = list
			};
		}
	}
}
