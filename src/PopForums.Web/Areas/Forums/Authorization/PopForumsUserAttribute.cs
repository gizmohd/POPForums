﻿using System;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PopForums.Models;
using PopForums.Services;

namespace PopForums.Web.Areas.Forums.Authorization
{
	public class PopForumsUserAttribute : IAuthorizationFilter, IActionFilter
	{
		public PopForumsUserAttribute(IUserService userService, IUserSessionService userSessionService)
		{
			_userService = userService;
			_userSessionService = userSessionService;
		}

		private readonly IUserService _userService;
		private readonly IUserSessionService _userSessionService;

		private bool _ignore;
		private User _user;

		protected virtual bool IsGlobalFilter()
		{
			return false;
		}

		public void OnAuthorization(AuthorizationFilterContext context)
		{
			// TODO: limit to pf controllers
			//if (!IsValidToRunOnController(context))
			//	return;
			//var attributes = context.ActionDescriptor.GetCustomAttributes(typeof(PopForumsAuthorizationIgnoreAttribute), false);
			//if (attributes.Length > 0)
			//{
			//	_ignore = true;
			//	return;
			//}
			//_ignore = false;
			if (context.HttpContext.User?.Identity?.Name == null)
				return;
			_user = _userService.GetUserByName(context.HttpContext.User.Identity.Name);
			if (_user != null)
			{
				foreach (var role in _user.Roles)
					((ClaimsIdentity)context.HttpContext.User.Identity).AddClaim(new Claim("ForumClaims", role));
				context.HttpContext.Items["PopForumsUser"] = _user;
				var profileService = context.HttpContext.RequestServices.GetService<IProfileService>();
				var profile = profileService.GetProfile(_user);
				context.HttpContext.Items["PopForumsProfile"] = profile;
			}
		}

		public void OnActionExecuting(ActionExecutingContext filterContext)
		{
			//if (_ignore || !IsValidToRunOnController(filterContext.Controller))
			//	return;

			if (filterContext.HttpContext.Response.StatusCode == StatusCodes.Status301MovedPermanently || filterContext.HttpContext.Response.StatusCode == StatusCodes.Status302Found)
				return;
			int cookieSessionID;
			int.TryParse(filterContext.HttpContext.Request.Cookies[UserSessionService._sessionIDCookieName], out cookieSessionID);
			int? sessionID = cookieSessionID == 0 ? (int?)null : cookieSessionID;
			var resultSessionID = _userSessionService.ProcessUserRequest(_user, sessionID, filterContext.HttpContext.Connection.RemoteIpAddress.ToString(), 
				() => filterContext.HttpContext.Response.Cookies.Delete(UserSessionService._sessionIDCookieName), 
				s => filterContext.HttpContext.Response.Cookies.Append(UserSessionService._sessionIDCookieName, s.ToString()));
		}

		public void OnActionExecuted(ActionExecutedContext filterContext)
		{
		}

		private bool IsValidToRunOnController(ControllerContext context)
		{
			if (IsGlobalFilter())
				return true;
			var controllerNamespace = context.ActionDescriptor.ControllerTypeInfo.Namespace;
			return controllerNamespace != null && controllerNamespace.StartsWith("PopForums");
		}
	}
}