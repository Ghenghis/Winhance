using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Tests.Integration
{
    [TestClass]
    public class DITest
    {
        private ServiceProvider? _serviceProvider;

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Register all services
            services.AddLogging();
            
            // Register FileManager services
            services.AddSingleton<IFileManagerService, FileManagerService>();
            services.AddSingleton<IBatchRenameService, BatchRenameService>();
            services.AddSingleton<IOrganizerService, OrganizerService>();
            services.AddSingleton<INexusIndexerService, NexusIndexerService>();
            services.AddSingleton<IAdvancedFileOperations, AdvancedFileOperationsService>();
            services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();
            services.AddSingleton<ISpaceAnalyzerService, SpaceAnalyzerService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<ITabService, TabService>();
            services.AddSingleton<IFavoritesService, FavoritesService>();
            services.AddSingleton<IOperationQueueService, OperationQueueService>();
            services.AddSingleton<IDriveDetectionService, DriveDetectionService>();
            services.AddSingleton<IBackupProtectionService, BackupProtectionService>();
            services.AddSingleton<ISelectionService, SelectionService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IViewModeService, ViewModeService>();
            services.AddSingleton<ISortingService, SortingService>();
            services.AddSingleton<IAddressBarService, AddressBarService>();
            services.AddSingleton<IQuickFilterService, QuickFilterService>();
            services.AddSingleton<IPreviewService, PreviewService>();
            services.AddSingleton<IQuickLookService, QuickLookService>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton<IArchiveService, ArchiveService>();
            services.AddSingleton<ICompareService, CompareService>();
            services.AddSingleton<ISyncService, SyncService>();
            services.AddSingleton<IWatchFolderService, WatchFolderService>();
            services.AddSingleton<IColumnService, ColumnService>();
            services.AddSingleton<IContextMenuService, ContextMenuService>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [TestMethod]
        public void All_FileManager_Services_Should_Resolve()
        {
            Assert.IsNotNull(_serviceProvider, "Service provider should not be null");

            var serviceTypes = new[]
            {
                typeof(IFileManagerService),
                typeof(IBatchRenameService),
                typeof(IOrganizerService),
                typeof(INexusIndexerService),
                typeof(IAdvancedFileOperations),
                typeof(IDuplicateFinderService),
                typeof(ISpaceAnalyzerService),
                typeof(ISearchService),
                typeof(ITabService),
                typeof(IFavoritesService),
                typeof(IOperationQueueService),
                typeof(IDriveDetectionService),
                typeof(IBackupProtectionService),
                typeof(ISelectionService),
                typeof(IClipboardService),
                typeof(IViewModeService),
                typeof(ISortingService),
                typeof(IAddressBarService),
                typeof(IQuickFilterService),
                typeof(IPreviewService),
                typeof(IQuickLookService),
                typeof(IMetadataService),
                typeof(IArchiveService),
                typeof(ICompareService),
                typeof(ISyncService),
                typeof(IWatchFolderService),
                typeof(IColumnService),
                typeof(IContextMenuService)
            };

            var failedServices = new List<string>();

            foreach (var serviceType in serviceTypes)
            {
                try
                {
                    var service = _serviceProvider.GetService(serviceType);
                    Assert.IsNotNull(service, $"Service {serviceType.Name} should not be null");
                }
                catch (Exception ex)
                {
                    failedServices.Add($"{serviceType.Name}: {ex.Message}");
                }
            }

            if (failedServices.Any())
            {
                Assert.Fail($"Failed to resolve services:\n{string.Join("\n", failedServices)}");
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            _serviceProvider?.Dispose();
        }
    }
}
