using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.TV;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Providers.Tests.TV;

public class EpisodeMetadataServiceTests
{
    [Fact]
    public async void RefreshMetadata_StateUnderTest_ExpectedResult()
    {
        IServerConfigurationManager serverConfigurationManager = Mock.Of<IServerConfigurationManager>(MockBehavior.Strict);

        var episodeMetadataProvider = new Mock<IRemoteMetadataProvider<Episode, EpisodeInfo>>(MockBehavior.Strict);
        episodeMetadataProvider.Setup(emp => emp.Name).Returns("Mock");
        episodeMetadataProvider.Setup(emp => emp.GetMetadata(It.IsAny<EpisodeInfo>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new MetadataResult<Episode>()
            {
                HasMetadata = true,
                Item = new Episode
                {
                    Overview = "New"
                }
            }));

        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        providerManager.Setup(pm => pm.GetImageProviders(It.IsAny<BaseItem>(), It.IsAny<ImageRefreshOptions>()))
            .Returns(Array.Empty<IImageProvider>());
        providerManager.Setup(pm => pm.GetMetadataProviders<Episode>(It.IsAny<BaseItem>(), It.IsAny<LibraryOptions>()))
            .Returns(new[] { episodeMetadataProvider.Object });

        IFileSystem fileSystem = Mock.Of<IFileSystem>(MockBehavior.Strict);

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(lm => lm.GetLibraryOptions(It.IsAny<BaseItem>()))
            .Returns(new LibraryOptions());

        var service = new EpisodeMetadataService(serverConfigurationManager, NullLogger<EpisodeMetadataService>.Instance, providerManager.Object, fileSystem, libraryManager.Object);

        BaseItem.Logger = NullLogger<BaseItem>.Instance;
        Video.LiveTvManager = Mock.Of<ILiveTvManager>();
        BaseItem.LibraryManager = libraryManager.Object;

        var item = new Episode
        {
            Overview = "Old"
        };
        // Either of these not set adds ItemUpdateType.MetadataImport
        item.PresentationUniqueKey = item.CreatePresentationUniqueKey();
        item.SeasonName = item.FindSeasonName(); // TODO why is find part of Episode instead of the metadata service itself?
        // keep from querying LibraryManager, ConfigurationManager
        item.PreferredMetadataCountryCode = "US";
        item.PreferredMetadataLanguage = "en";

        IDirectoryService directoryService = Mock.Of<IDirectoryService>(MockBehavior.Strict);
        var metadataRefreshOptions = new MetadataRefreshOptions(directoryService)
        {
            MetadataRefreshMode = MetadataRefreshMode.Default // None
        };
        var updated = await service.RefreshMetadata(item, metadataRefreshOptions, CancellationToken.None);

        // MetadataDownload hit by provider returning metadata, MetadataRefreshMode.None/ValidationOnly result in no update
        Assert.Equal(ItemUpdateType.None | ItemUpdateType.MetadataDownload, updated);
        Assert.Equal("New", item.Overview);
    }
}
