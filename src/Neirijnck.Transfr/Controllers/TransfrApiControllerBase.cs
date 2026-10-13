using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Neirijnck.Transfr.Controllers;

/// <summary>
/// Base for all Transfr endpoints: /umbraco/management/api/v1/transfr/..., admins only, 404 when no remote is
/// configured, documented in the separate "transfr" OpenAPI document.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("transfr")]
[MapToApi(Constants.ApiName)]
[ApiExplorerSettings(GroupName = "Transfr")]
[Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]
[RequireRemotes]
public abstract class TransfrApiControllerBase : ManagementApiControllerBase
{
}