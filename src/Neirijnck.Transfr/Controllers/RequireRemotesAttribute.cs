using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Neirijnck.Transfr.Remote;

namespace Neirijnck.Transfr.Controllers;

/// <summary>
/// Answers 404 when no remote is configured: an environment without remotes exposes no Transfr endpoints at all.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequireRemotesAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.HttpContext.RequestServices.GetRequiredService<RemoteRegistry>().AnyConfigured)
        {
            context.Result = new NotFoundResult();
        }
    }
}