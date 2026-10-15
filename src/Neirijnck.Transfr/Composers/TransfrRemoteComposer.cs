using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Configuration;
using Neirijnck.Transfr.Local;
using Neirijnck.Transfr.Persistence;
using Neirijnck.Transfr.Planning;
using Neirijnck.Transfr.Remote;
using Neirijnck.Transfr.Transfer;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Neirijnck.Transfr.Composers;

/// <summary>
/// Registers the remote configuration, the client that talks to remotes, the readers, and the compare and plan services.
/// </summary>
public class TransfrRemoteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<TransfrOptions>(builder.Config.GetSection(TransfrOptions.Section));
        builder.Services.AddSingleton<RuntimeRemoteStore>();
        builder.Services.AddSingleton<RemoteRegistry>();
        builder.Services.AddHttpClient(RemoteClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RemoteClient>();
        builder.Services.AddSingleton<RemoteTreeReader>();
        builder.Services.AddSingleton<RemoteDocumentReader>();
        builder.Services.AddSingleton<LocalTreeReader>();
        builder.Services.AddSingleton<LocalDocumentReader>();
        builder.Services.AddSingleton<LocalSchemaNames>();
        builder.Services.AddSingleton<LocalSchemaReader>();
        builder.Services.AddSingleton<RemoteSchemaReader>();
        builder.Services.AddSingleton<SchemaCheckService>();
        builder.Services.AddSingleton<TreeCompareService>();
        builder.Services.AddSingleton<DocumentDiffService>();
        builder.Services.AddSingleton<CopyHistoryStore>();
        builder.Services.AddSingleton<CopyPlanService>();
        builder.Services.AddSingleton<TransferService>();
    }
}