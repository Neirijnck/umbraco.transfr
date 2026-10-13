using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace Neirijnck.Transfr.TestSite.Seeding;

public sealed class TestContentSeederComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, TestContentSeeder>();
}