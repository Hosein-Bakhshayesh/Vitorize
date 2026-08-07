using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Vitorize.Api.Hosting;
using Xunit;

namespace Vitorize.Tests;

public sealed class HostingStoragePathsTests
{
    [Fact]
    public void Storage_is_application_owned_and_created_without_host_path_configuration()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "VitorizeTests", Guid.NewGuid().ToString("N"));
        try
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.ContentRootPath.Returns(contentRoot);
            var paths = new HostingStoragePaths(environment, new ConfigurationBuilder().Build());

            paths.ValidateAndPrepare();

            Assert.Equal(Path.Combine(contentRoot, "App_Data", "DataProtection"), paths.DataProtectionKeysPath);
            Assert.Equal(Path.Combine(contentRoot, "App_Data", "PublicMedia"), paths.PublicMediaRoot);
            Assert.Equal(Path.Combine(contentRoot, "App_Data", "PrivateDocuments"), paths.PrivateDocumentsRoot);
            Assert.True(Directory.Exists(paths.DataProtectionKeysPath));
            Assert.True(Directory.Exists(paths.PublicMediaRoot));
            Assert.True(Directory.Exists(paths.PrivateDocumentsRoot));
            Assert.DoesNotContain("wwwroot", paths.PrivateDocumentsRoot, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }

}
