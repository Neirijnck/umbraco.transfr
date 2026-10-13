using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Neirijnck.Transfr.Composers;

/// <summary>
/// Registers the package's own OpenAPI document (source for the generated TypeScript client).
/// </summary>
public class TransfrApiComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IOperationIdHandler, TransfrOperationIdHandler>();

        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            options.SwaggerDoc(Constants.ApiName, new OpenApiInfo { Title = "Transfr Backoffice API", Version = "1.0" });
            options.OperationFilter<TransfrOperationSecurityFilter>();
        });
    }

    public class TransfrOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }

    /// <summary>
    /// Operation ids = action names, so the generated client gets short method names.
    /// </summary>
    public class TransfrOperationIdHandler : OperationIdHandler
    {
        public TransfrOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
            : base(apiVersioningOptions)
        {
        }

        protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
            => controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith("Neirijnck.Transfr.Controllers", StringComparison.Ordinal) is true;

        public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
    }
}