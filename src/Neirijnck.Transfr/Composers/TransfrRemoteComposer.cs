using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neirijnck.Transfr.Configuration;
using Neirijnck.Transfr.Remote;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Neirijnck.Transfr.Composers;

/// <summary>
/// Registers the remote configuration and the client that talks to remotes.
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
    }
}