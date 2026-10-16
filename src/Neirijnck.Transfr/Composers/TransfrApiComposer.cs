using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Neirijnck.Transfr.Composers;

/// <summary>
/// Registers the package's own OpenAPI document (source for the generated TypeScript client).
/// </summary>
public class TransfrApiComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddBackOfficeOpenApiDocument(Constants.ApiName, document => document
            .WithTitle("Transfr Backoffice API")
            .WithBackOfficeAuthentication()
            // The controllers answer with the backoffice JSON options, so the schema must describe those.
            .WithJsonOptions(UmbracoConstants.JsonOptionsNames.BackOffice)
            // Runs after Umbraco's own operation id transformer, which the document adds first.
            .ConfigureOpenApiOptions(options => options.AddOperationTransformer<TransfrOperationIdTransformer>()));

    /// <summary>
    /// Operation ids = action names, so the generated client gets short method names.
    /// </summary>
    public sealed class TransfrOperationIdTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(context);

            if (context.Description.ActionDescriptor is ControllerActionDescriptor action
                && action.ControllerTypeInfo.Namespace?.StartsWith("Neirijnck.Transfr.Controllers", StringComparison.Ordinal) is true)
            {
                operation.OperationId = action.ActionName;
            }

            return Task.CompletedTask;
        }
    }
}