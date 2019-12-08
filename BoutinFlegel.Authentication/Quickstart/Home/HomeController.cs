// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace IdentityServer4.Quickstart.UI
{
	[SecurityHeaders]
	[AllowAnonymous]
	public class HomeController : Controller
	{
		private IIdentityServerInteractionService Interaction { get; }
		private IWebHostEnvironment Environment { get; }
		private ILogger Logger { get; }

		public HomeController(IIdentityServerInteractionService interaction, IWebHostEnvironment environment, ILogger<HomeController> logger)
		{
			Interaction = interaction;
			Environment = environment;
			Logger = logger;
		}

		public IActionResult Index()
		{
			if (Environment.IsDevelopment())
			{
				// only show in development
				return View();
			}

			Logger.LogInformation("Homepage is disabled in production. Returning 404.");
			return NotFound();
		}

		/// <summary>
		/// Shows the error page
		/// </summary>
		public async Task<IActionResult> Error(string errorId)
		{
			var vm = new ErrorViewModel();

			// retrieve error details from identityserver
			var message = await Interaction.GetErrorContextAsync(errorId);
			if (message != null)
			{
				vm.Error = message;

				if (!Environment.IsDevelopment())
				{
					// only show in development
					message.ErrorDescription = null;
				}
			}

			return View("Error", vm);
		}
	}
}
