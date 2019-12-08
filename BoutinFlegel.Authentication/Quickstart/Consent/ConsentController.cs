// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4.Events;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer4.Quickstart.UI
{
	/// <summary>
	/// This controller processes the consent UI
	/// </summary>
	[SecurityHeaders]
	[Authorize]
	public class ConsentController : Controller
	{
		private IIdentityServerInteractionService Interaction { get; }
		private IClientStore ClientStore { get; }
		private IResourceStore ResourceStore { get; }
		private IEventService Events { get; }
		private ILogger<ConsentController> Logger { get; }

		public ConsentController(
			IIdentityServerInteractionService interaction,
			IClientStore clientStore,
			IResourceStore resourceStore,
			IEventService events,
			ILogger<ConsentController> logger)
		{
			Interaction = interaction;
			ClientStore = clientStore;
			ResourceStore = resourceStore;
			Events = events;
			Logger = logger;
		}

		/// <summary>
		/// Shows the consent screen
		/// </summary>
		/// <param name="returnUrl"></param>
		/// <returns></returns>
		[HttpGet]
		public async Task<IActionResult> Index(string returnUrl)
		{
			var vm = await BuildViewModelAsync(returnUrl);
			return vm != null ? View("Index", vm) : View("Error");
		}

		/// <summary>
		/// Handles the consent screen postback
		/// </summary>
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Index(ConsentInputModel model)
		{
			var result = await ProcessConsent(model);

			if (result.IsRedirect)
			{
				if (await ClientStore.IsPkceClientAsync(result.ClientId))
				{
					// if the client is PKCE then we assume it's native, so this change in how to
					// return the response is for better UX for the end user.
					return View("Redirect", new RedirectViewModel { RedirectUrl = result.RedirectUri });
				}

				return Redirect(result.RedirectUri);
			}

			if (result.HasValidationError)
			{
				ModelState.AddModelError(string.Empty, result.ValidationError);
			}

			return result.ShowView ? View("Index", result.ViewModel) : View("Error");
		}

		/*****************************************/
		/* helper APIs for the ConsentController */
		/*****************************************/
		private async Task<ProcessConsentResult> ProcessConsent(ConsentInputModel model)
		{
			var result = new ProcessConsentResult();

			// validate return url is still valid
			var request = await Interaction.GetAuthorizationContextAsync(model.ReturnUrl);
			if (request == null) return result;

			ConsentResponse grantedConsent = null;

			// user clicked 'no' - send back the standard 'access_denied' response
			if (model?.Button == "no")
			{
				grantedConsent = ConsentResponse.Denied;

				// emit event
				await Events.RaiseAsync(new ConsentDeniedEvent(User.GetSubjectId(), request.ClientId, request.ScopesRequested));
			}
			// user clicked 'yes' - validate the data
			else if (model?.Button == "yes")
			{
				// if the user consented to some scope, build the response model
				if (model.ScopesConsented != null && model.ScopesConsented.Any())
				{
					var scopes = model.ScopesConsented;
					if (ConsentOptions.EnableOfflineAccess == false)
					{
						scopes = scopes.Where(x => x != IdentityServer4.IdentityServerConstants.StandardScopes.OfflineAccess);
					}

					grantedConsent = new ConsentResponse
					{
						RememberConsent = model.RememberConsent,
						ScopesConsented = scopes.ToArray()
					};

					// emit event
					await Events.RaiseAsync(new ConsentGrantedEvent(User.GetSubjectId(), request.ClientId, request.ScopesRequested, grantedConsent.ScopesConsented, grantedConsent.RememberConsent));
				}
				else
				{
					result.ValidationError = ConsentOptions.MustChooseOneErrorMessage;
				}
			}
			else
			{
				result.ValidationError = ConsentOptions.InvalidSelectionErrorMessage;
			}

			if (grantedConsent != null)
			{
				// communicate outcome of consent back to identityserver
				await Interaction.GrantConsentAsync(request, grantedConsent);

				// indicate that's it ok to redirect back to authorization endpoint
				result.RedirectUri = model.ReturnUrl;
				result.ClientId = request.ClientId;
			}
			else
			{
				// we need to redisplay the consent UI
				result.ViewModel = await BuildViewModelAsync(model.ReturnUrl, model);
			}

			return result;
		}

		private async Task<ConsentViewModel> BuildViewModelAsync(string returnUrl, ConsentInputModel model = null)
		{
			var request = await Interaction.GetAuthorizationContextAsync(returnUrl);
			if (request != null)
			{
				var client = await ClientStore.FindEnabledClientByIdAsync(request.ClientId);
				if (client != null)
				{
					var resources = await ResourceStore.FindEnabledResourcesByScopeAsync(request.ScopesRequested);
					if (resources != null && (resources.IdentityResources.Any() || resources.ApiResources.Any()))
					{
						return CreateConsentViewModel(model, returnUrl, request, client, resources);
					}
					else
					{
						Logger.LogError("No scopes matching: {0}", request.ScopesRequested.Aggregate((x, y) => x + ", " + y));
					}
				}
				else
				{
					Logger.LogError("Invalid client id: {0}", request.ClientId);
				}
			}
			else
			{
				Logger.LogError("No consent request matching request: {0}", returnUrl);
			}

			return null;
		}

		private ConsentViewModel CreateConsentViewModel(
			ConsentInputModel model, string returnUrl,
			AuthorizationRequest request,
			Client client, Resources resources)
		{
			var vm = new ConsentViewModel
			{
				RememberConsent = model?.RememberConsent ?? true,
				ScopesConsented = model?.ScopesConsented ?? Enumerable.Empty<string>(),

				ReturnUrl = returnUrl,

				ClientName = client.ClientName ?? client.ClientId,
				ClientUrl = client.ClientUri,
				ClientLogoUrl = client.LogoUri,
				AllowRememberConsent = client.AllowRememberConsent
			};

			vm.IdentityScopes = resources.IdentityResources.Select(x => CreateScopeViewModel(x, vm.ScopesConsented.Contains(x.Name) || model == null)).ToArray();
			vm.ResourceScopes = resources.ApiResources.SelectMany(x => x.Scopes).Select(x => CreateScopeViewModel(x, vm.ScopesConsented.Contains(x.Name) || model == null)).ToArray();
			if (ConsentOptions.EnableOfflineAccess && resources.OfflineAccess)
			{
				vm.ResourceScopes = vm.ResourceScopes.Union(new ScopeViewModel[] {
					GetOfflineAccessScope(vm.ScopesConsented.Contains(IdentityServer4.IdentityServerConstants.StandardScopes.OfflineAccess) || model == null)
				});
			}

			return vm;
		}

		private ScopeViewModel CreateScopeViewModel(IdentityResource identity, bool check) => new ScopeViewModel
		{
			Name = identity.Name,
			DisplayName = identity.DisplayName,
			Description = identity.Description,
			Emphasize = identity.Emphasize,
			Required = identity.Required,
			Checked = check || identity.Required
		};

		public ScopeViewModel CreateScopeViewModel(Scope scope, bool check) => new ScopeViewModel
		{
			Name = scope.Name,
			DisplayName = scope.DisplayName,
			Description = scope.Description,
			Emphasize = scope.Emphasize,
			Required = scope.Required,
			Checked = check || scope.Required
		};

		private ScopeViewModel GetOfflineAccessScope(bool check) => new ScopeViewModel
		{
			Name = IdentityServerConstants.StandardScopes.OfflineAccess,
			DisplayName = ConsentOptions.OfflineAccessDisplayName,
			Description = ConsentOptions.OfflineAccessDescription,
			Emphasize = true,
			Checked = check
		};
	}
}
