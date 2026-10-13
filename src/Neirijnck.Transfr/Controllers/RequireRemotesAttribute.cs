using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Controllers;

/// <summary>
/// Answers 404 unless Transfr is enabled here (a remote is configured, or runtime remotes are allowed): an environment
/// without either exposes no Transfr endpoints at all.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequireRemotesAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.HttpContext.RequestServices.GetRequiredService<RemoteRegistry>().IsEnabled)
        {
            context.Result = new NotFoundResult();
        }
    }
}