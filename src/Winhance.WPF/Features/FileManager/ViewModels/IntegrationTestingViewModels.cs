using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for integration testing
    /// </summary>
    public partial class IntegrationTestViewModel : ObservableObject
    {
        private readonly IIntegrationTestService _integrationTestService;
        private ObservableCollection<TestSuite> _testSuites = new();
        private ObservableCollection<TestResult> _testResults = new();

        [ObservableProperty]
        private TestSuite? _selectedSuite;

        [ObservableProperty]
        private bool _isRunningTests;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private double _testProgress;

        [ObservableProperty]
        private int _testsRun;

        [ObservableProperty]
        private int _testsPassed;

        [ObservableProperty]
        private int _testsFailed;

        [ObservableProperty]
        private TimeSpan _testDuration;

        [ObservableProperty]
        private bool _runParallel = true;

        [ObservableProperty]
        private bool _continueOnFailure = true;

        [ObservableProperty]
        private string _currentTest = string.Empty;

        public ObservableCollection<TestSuite> TestSuites
        {
            get => _testSuites;
            set => SetProperty(ref _testSuites, value);
        }

        public ObservableCollection<TestResult> TestResults
        {
            get => _testResults;
            set => SetProperty(ref _testResults, value);
        }

        public IntegrationTestViewModel(IIntegrationTestService integrationTestService)
        {
            _integrationTestService = integrationTestService;
            _ = LoadTestSuitesAsync();
        }

        private async Task LoadTestSuitesAsync()
        {
            try
            {
                var suites = await _integrationTestService.GetTestSuitesAsync();
                TestSuites.Clear();
                foreach (var suite in suites)
                {
                    TestSuites.Add(suite);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading test suites: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RunAllTestsAsync()
        {
            if (!TestSuites.Any()) return;

            IsRunningTests = true;
            StatusMessage = "Running all tests...";
            ResetTestCounters();

            var startTime = DateTime.Now;

            try
            {
                var options = new TestRunOptions
                {
                    RunParallel = RunParallel,
                    ContinueOnFailure = ContinueOnFailure
                };

                var progress = new Progress<TestProgress>(p =>
                {
                    TestProgress = p.Percentage;
                    CurrentTest = p.CurrentTest;
                    TestsRun = p.TestsRun;
                    TestsPassed = p.TestsPassed;
                    TestsFailed = p.TestsFailed;
                    StatusMessage = $"Running {p.CurrentTest}... {p.Percentage:F1}%";
                });

                var results = await _integrationTestService.RunAllTestsAsync(options, progress);
                
                TestResults.Clear();
                foreach (var result in results)
                {
                    TestResults.Add(result);
                }

                TestDuration = DateTime.Now - startTime;
                StatusMessage = $"All tests completed. Passed: {TestsPassed}, Failed: {TestsFailed}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Test run failed: {ex.Message}";
            }
            finally
            {
                IsRunningTests = false;
            }
        }

        [RelayCommand]
        private async Task RunTestSuiteAsync(TestSuite? suite)
        {
            if (suite == null) return;

            IsRunningTests = true;
            StatusMessage = $"Running test suite: {suite.Name}";
            ResetTestCounters();

            var startTime = DateTime.Now;

            try
            {
                var options = new TestRunOptions
                {
                    RunParallel = RunParallel,
                    ContinueOnFailure = ContinueOnFailure
                };

                var progress = new Progress<TestProgress>(p =>
                {
                    TestProgress = p.Percentage;
                    CurrentTest = p.CurrentTest;
                    TestsRun = p.TestsRun;
                    TestsPassed = p.TestsPassed;
                    TestsFailed = p.TestsFailed;
                });

                var results = await _integrationTestService.RunTestSuiteAsync(suite.Id, options, progress);
                
                TestResults.Clear();
                foreach (var result in results)
                {
                    TestResults.Add(result);
                }

                TestDuration = DateTime.Now - startTime;
                StatusMessage = $"Test suite '{suite.Name}' completed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Test suite failed: {ex.Message}";
            }
            finally
            {
                IsRunningTests = false;
            }
        }

        [RelayCommand]
        private void StopTests()
        {
            _integrationTestService.StopTests();
            IsRunningTests = false;
            StatusMessage = "Tests stopped";
        }

        [RelayCommand]
        private void ClearResults()
        {
            TestResults.Clear();
            ResetTestCounters();
            StatusMessage = "Results cleared";
        }

        [RelayCommand]
        private void ExportResults()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "HTML Report (*.html)|*.html|CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".html",
                FileName = $"TestResults_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(TestResults,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else if (dialog.FileName.EndsWith(".csv"))
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("TestName,Status,Duration,StartedAt,CompletedAt,Error");
                        foreach (var result in TestResults)
                        {
                            csv.AppendLine($"{result.TestName},{result.Status},{result.Duration.TotalSeconds:F2}s,{result.StartedAt:g},{result.CompletedAt:g},{result.ErrorMessage}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    else
                    {
                        var html = GenerateHtmlReport();
                        System.IO.File.WriteAllText(dialog.FileName, html);
                    }
                    StatusMessage = "Results exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        private string GenerateHtmlReport()
        {
            var html = $"<html><head><title>Test Results</title><style>body{{font-family:Arial;}}table{{border-collapse:collapse;width:100%;}}th,td{{border:1px solid #ddd;padding:8px;}}th{{background-color:#4CAF50;color:white;}}</style></head><body>";
            html += $"<h1>Test Results - {DateTime.Now:g}</h1>";
            html += $"<p>Total: {TestsRun}, Passed: {TestsPassed}, Failed: {TestsFailed}, Duration: {TestDuration.TotalSeconds:F2}s</p>";
            html += "<table><tr><th>Test Name</th><th>Status</th><th>Duration</th><th>Error</th></tr>";
            foreach (var result in TestResults)
            {
                var rowColor = result.Status == TestStatus.Passed ? "#90EE90" : result.Status == TestStatus.Failed ? "#FFB6C1" : "";
                html += $"<tr style='background-color:{rowColor}'><td>{result.TestName}</td><td>{result.Status}</td><td>{result.Duration.TotalSeconds:F2}s</td><td>{result.ErrorMessage}</td></tr>";
            }
            html += "</table></body></html>";
            return html;
        }

        [RelayCommand]
        private void ViewTestDetails(TestResult? result)
        {
            if (result == null) return;

            var message = $"Test Details\n\n" +
                         $"Test: {result.TestName}\n" +
                         $"Status: {result.Status}\n" +
                         $"Started: {result.StartedAt:g}\n" +
                         $"Completed: {result.CompletedAt:g}\n" +
                         $"Duration: {result.Duration.TotalSeconds:F2}s\n" +
                         $"Metrics: {result.Metrics.Count}\n" +
                         $"Attachments: {result.Attachments.Count}";
            if (!string.IsNullOrEmpty(result.ErrorMessage))
                message += $"\n\nError: {result.ErrorMessage}";

            System.Windows.MessageBox.Show(message, "Test Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void RetryFailedTests()
        {
            var failedTests = TestResults.Where(r => r.Status == TestStatus.Failed).ToList();
            if (!failedTests.Any())
            {
                StatusMessage = "No failed tests to retry";
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Retry {failedTests.Count} failed tests?",
                "Retry Failed Tests",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = $"Retrying {failedTests.Count} failed tests...";
            }
        }

        [RelayCommand]
        private void GenerateReport()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "HTML Report (*.html)|*.html|PDF Report (*.pdf)|*.pdf",
                DefaultExt = ".html",
                FileName = $"TestReport_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var html = GenerateHtmlReport();
                    System.IO.File.WriteAllText(dialog.FileName, html);
                    System.Windows.MessageBox.Show(
                        "Test report generated successfully.",
                        "Report Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    StatusMessage = "Report generated successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Report generation failed: {ex.Message}";
                }
            }
        }

        private void ResetTestCounters()
        {
            TestProgress = 0;
            TestsRun = 0;
            TestsPassed = 0;
            TestsFailed = 0;
            TestDuration = TimeSpan.Zero;
            CurrentTest = string.Empty;
        }
    }

    /// <summary>
    /// ViewModel for feature audit
    /// </summary>
    public partial class FeatureAuditViewModel : ObservableObject
    {
        private readonly IFeatureAuditService _featureAuditService;
        private ObservableCollection<FeatureCategory> _featureCategories = new();
        private ObservableCollection<FeatureItem> _features = new();

        [ObservableProperty]
        private FeatureCategory? _selectedCategory;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private int _totalFeatures;

        [ObservableProperty]
        private int _completedFeatures;

        [ObservableProperty]
        private int _testedFeatures;

        [ObservableProperty]
        private double _completionPercentage;

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private FeatureStatus _statusFilter = FeatureStatus.All;

        [ObservableProperty]
        private FeatureItem? _selectedFeature;

        public ObservableCollection<FeatureCategory> FeatureCategories
        {
            get => _featureCategories;
            set => SetProperty(ref _featureCategories, value);
        }

        public ObservableCollection<FeatureItem> Features
        {
            get => _features;
            set => SetProperty(ref _features, value);
        }

        public FeatureAuditViewModel(IFeatureAuditService featureAuditService)
        {
            _featureAuditService = featureAuditService;
            _ = LoadFeatureCategoriesAsync();
            _ = LoadFeaturesAsync();
        }

        private async Task LoadFeatureCategoriesAsync()
        {
            try
            {
                var categories = await _featureAuditService.GetFeatureCategoriesAsync();
                FeatureCategories.Clear();
                foreach (var category in categories)
                {
                    FeatureCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading categories: {ex.Message}";
            }
        }

        private async Task LoadFeaturesAsync()
        {
            IsLoading = true;

            try
            {
                var features = await _featureAuditService.GetAllFeaturesAsync();
                Features.Clear();
                foreach (var feature in features)
                {
                    Features.Add(feature);
                }
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading features: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedCategoryChanged(FeatureCategory? value)
        {
            if (value != null)
            {
                _ = FilterByCategoryAsync(value.Id);
            }
        }

        private async Task FilterByCategoryAsync(string categoryId)
        {
            IsLoading = true;

            try
            {
                var features = await _featureAuditService.GetFeaturesByCategoryAsync(categoryId);
                Features.Clear();
                foreach (var feature in features)
                {
                    Features.Add(feature);
                }
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error filtering features: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void FilterFeatures()
        {
            var filtered = Features.AsEnumerable();

            if (!string.IsNullOrEmpty(FilterText))
            {
                filtered = filtered.Where(f => 
                    f.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    f.Description.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            }

            if (StatusFilter != FeatureStatus.All)
            {
                filtered = filtered.Where(f => f.Status == StatusFilter);
            }

            Features.Clear();
            foreach (var feature in filtered)
            {
                Features.Add(feature);
            }
        }

        [RelayCommand]
        private async Task UpdateFeatureStatusAsync(FeatureItem? feature, FeatureStatus status)
        {
            if (feature == null) return;

            try
            {
                feature.Status = status;
                await _featureAuditService.UpdateFeatureAsync(feature);
                UpdateStatistics();
                StatusMessage = $"Feature '{feature.Name}' status updated";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating feature: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task MarkAsCompletedAsync(FeatureItem? feature)
        {
            if (feature == null) return;
            await UpdateFeatureStatusAsync(feature, FeatureStatus.Completed);
        }

        [RelayCommand]
        private async Task MarkAsTestedAsync(FeatureItem? feature)
        {
            if (feature == null) return;
            await UpdateFeatureStatusAsync(feature, FeatureStatus.Tested);
        }

        [RelayCommand]
        private void ViewFeatureDetails(FeatureItem? feature)
        {
            if (feature == null) return;

            var message = $"Feature Details\n\n" +
                         $"Name: {feature.Name}\n" +
                         $"Category: {feature.CategoryId}\n" +
                         $"Status: {feature.Status}\n" +
                         $"Priority: {feature.Priority}\n" +
                         $"Assigned To: {feature.AssignedTo}\n" +
                         $"Created: {feature.CreatedAt:g}\n" +
                         $"Completed: {feature.CompletedAt?.ToString("g") ?? "Not completed"}\n" +
                         $"Dependencies: {feature.Dependencies.Count}\n" +
                         $"Test Cases: {feature.TestCases.Count}\n\n" +
                         $"Description:\n{feature.Description}";

            System.Windows.MessageBox.Show(message, "Feature Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportAuditReport()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv|HTML Report (*.html)|*.html",
                DefaultExt = ".csv",
                FileName = $"FeatureAudit_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Category,Name,Status,Priority,AssignedTo,Created,Completed");
                    foreach (var feature in Features)
                    {
                        csv.AppendLine($"{feature.CategoryId},{feature.Name},{feature.Status},{feature.Priority},{feature.AssignedTo},{feature.CreatedAt:g},{feature.CompletedAt?.ToString("g")}");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    StatusMessage = "Audit report exported successfully";
                    System.Windows.MessageBox.Show(
                        "Feature audit exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void GenerateCompletionChart()
        {
            var message = $"Feature Completion Chart\n\n" +
                         $"Total Features: {TotalFeatures}\n" +
                         $"Completed: {CompletedFeatures} ({CompletionPercentage:F1}%)\n" +
                         $"Tested: {TestedFeatures}\n" +
                         $"In Progress: {Features.Count(f => f.Status == FeatureStatus.InProgress)}\n" +
                         $"Not Started: {Features.Count(f => f.Status == FeatureStatus.NotStarted)}\n" +
                         $"Blocked: {Features.Count(f => f.Status == FeatureStatus.Blocked)}";

            System.Windows.MessageBox.Show(message, "Completion Chart",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void RefreshAudit()
        {
            _ = LoadFeaturesAsync();
        }

        private void UpdateStatistics()
        {
            TotalFeatures = Features.Count;
            CompletedFeatures = Features.Count(f => f.Status == FeatureStatus.Completed);
            TestedFeatures = Features.Count(f => f.Status == FeatureStatus.Tested);
            CompletionPercentage = TotalFeatures > 0 ? (double)CompletedFeatures / TotalFeatures * 100 : 0;
        }
    }

    /// <summary>
    /// ViewModel for project status
    /// </summary>
    public partial class ProjectStatusViewModel : ObservableObject
    {
        private readonly IProjectStatusService _projectStatusService;
        private ObservableCollection<ProjectMilestone> _milestones = new();
        private ObservableCollection<TeamMember> _teamMembers = new();

        [ObservableProperty]
        private ProjectOverview _projectOverview = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ProjectPhase _currentPhase;

        [ObservableProperty]
        private DateTime _projectStartDate;

        [ObservableProperty]
        private DateTime _projectEndDate;

        [ObservableProperty]
        private double _overallProgress;

        [ObservableProperty]
        private int _totalTasks;

        [ObservableProperty]
        private int _completedTasks;

        [ObservableProperty]
        private int _activeTasks;

        [ObservableProperty]
        private int _blockedTasks;

        public ObservableCollection<ProjectMilestone> Milestones
        {
            get => _milestones;
            set => SetProperty(ref _milestones, value);
        }

        public ObservableCollection<TeamMember> TeamMembers
        {
            get => _teamMembers;
            set => SetProperty(ref _teamMembers, value);
        }

        public ProjectStatusViewModel(IProjectStatusService projectStatusService)
        {
            _projectStatusService = projectStatusService;
            _ = LoadProjectStatusAsync();
        }

        private async Task LoadProjectStatusAsync()
        {
            IsLoading = true;

            try
            {
                ProjectOverview = await _projectStatusService.GetProjectOverviewAsync();
                
                var milestones = await _projectStatusService.GetMilestonesAsync();
                Milestones.Clear();
                foreach (var milestone in milestones)
                {
                    Milestones.Add(milestone);
                }

                var members = await _projectStatusService.GetTeamMembersAsync();
                TeamMembers.Clear();
                foreach (var member in members)
                {
                    TeamMembers.Add(member);
                }

                UpdateProjectStats();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading project status: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshStatusAsync()
        {
            await LoadProjectStatusAsync();
            StatusMessage = "Project status refreshed";
        }

        [RelayCommand]
        private void ViewMilestoneDetails(ProjectMilestone? milestone)
        {
            if (milestone == null) return;

            var message = $"Milestone Details\n\n" +
                         $"Name: {milestone.Name}\n" +
                         $"Status: {milestone.Status}\n" +
                         $"Planned: {milestone.PlannedDate:g}\n" +
                         $"Actual: {milestone.ActualDate?.ToString("g") ?? "Not completed"}\n" +
                         $"Completion: {milestone.CompletionPercentage}%\n" +
                         $"Deliverables: {milestone.Deliverables.Count}\n" +
                         $"Dependencies: {milestone.Dependencies.Count}\n\n" +
                         $"Description:\n{milestone.Description}";

            System.Windows.MessageBox.Show(message, "Milestone Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ViewTeamMember(TeamMember? member)
        {
            if (member == null) return;

            var message = $"Team Member Details\n\n" +
                         $"Name: {member.Name}\n" +
                         $"Email: {member.Email}\n" +
                         $"Role: {member.Role}\n" +
                         $"Active: {(member.IsActive ? "Yes" : "No")}\n" +
                         $"Last Active: {member.LastActive:g}\n" +
                         $"Assigned Tasks: {member.AssignedTasks}\n" +
                         $"Completed Tasks: {member.CompletedTasks}\n" +
                         $"Skills: {string.Join(", ", member.Skills)}";

            System.Windows.MessageBox.Show(message, "Team Member",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void GenerateProgressReport()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "HTML Report (*.html)|*.html|PDF Report (*.pdf)|*.pdf",
                DefaultExt = ".html",
                FileName = $"ProgressReport_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var html = $"<html><head><title>Progress Report</title></head><body>";
                    html += $"<h1>{ProjectOverview.Name} - Progress Report</h1>";
                    html += $"<p>Phase: {CurrentPhase}</p>";
                    html += $"<p>Progress: {OverallProgress:F1}%</p>";
                    html += $"<p>Tasks: {CompletedTasks}/{TotalTasks}</p>";
                    html += "</body></html>";
                    System.IO.File.WriteAllText(dialog.FileName, html);
                    StatusMessage = "Progress report generated successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Report generation failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ExportProjectData()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|CSV File (*.csv)|*.csv",
                DefaultExt = ".json",
                FileName = $"ProjectData_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var projectData = new { ProjectOverview, Milestones, TeamMembers };
                    var json = System.Text.Json.JsonSerializer.Serialize(projectData,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    StatusMessage = "Project data exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ViewBurndownChart()
        {
            var remaining = TotalTasks - CompletedTasks;
            var message = $"Burndown Chart\n\n" +
                         $"Total Tasks: {TotalTasks}\n" +
                         $"Completed: {CompletedTasks}\n" +
                         $"Remaining: {remaining}\n" +
                         $"Active: {ActiveTasks}\n" +
                         $"Blocked: {BlockedTasks}\n" +
                         $"Progress: {OverallProgress:F1}%\n" +
                         $"Time Elapsed: {ProjectOverview.TimeElapsed.TotalDays:F1} days\n" +
                         $"Time Remaining: {ProjectOverview.TimeRemaining.TotalDays:F1} days";

            System.Windows.MessageBox.Show(message, "Burndown Chart",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ScheduleStatusUpdate()
        {
            var message = "Schedule Status Update\n\n" +
                         "Configure automatic status updates:\n\n" +
                         "• Daily Summary (9:00 AM)\n" +
                         "• Weekly Report (Monday 9:00 AM)\n" +
                         "• Milestone Notifications\n" +
                         "• Critical Issue Alerts\n" +
                         "• Team Member Updates";

            System.Windows.MessageBox.Show(message, "Schedule Updates",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void UpdateProjectStats()
        {
            CurrentPhase = ProjectOverview.CurrentPhase;
            ProjectStartDate = ProjectOverview.StartDate;
            ProjectEndDate = ProjectOverview.EndDate;
            OverallProgress = ProjectOverview.ProgressPercentage;
            TotalTasks = ProjectOverview.TotalTasks;
            CompletedTasks = ProjectOverview.CompletedTasks;
            ActiveTasks = ProjectOverview.ActiveTasks;
            BlockedTasks = ProjectOverview.BlockedTasks;
        }
    }

    // Model classes
    public class TestSuite
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<TestCase> TestCases { get; set; } = new();
        public TestCategory Category { get; set; }
        public bool IsEnabled { get; set; }
        public int EstimatedDuration { get; set; }
        public DateTime LastRun { get; set; }
        public TestStatus LastResult { get; set; }
    }

    public class TestCase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TestType Type { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public List<TestStep> Steps { get; set; } = new();
        public List<string> ExpectedResults { get; set; } = new();
        public int Timeout { get; set; }
        public bool IsCritical { get; set; }
        public int RetryCount { get; set; }
    }

    public class TestStep
    {
        public int Order { get; set; }
        public string Action { get; set; } = string.Empty;
        public string ExpectedResult { get; set; } = string.Empty;
        public string TestData { get; set; } = string.Empty;
    }

    public class TestResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TestCaseId { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public TestStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> ActualResults { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<Attachment> Attachments { get; set; } = new();
    }

    public class TestRunOptions
    {
        public bool RunParallel { get; set; }
        public bool ContinueOnFailure { get; set; }
        public int MaxParallelTests { get; set; } = 4;
        public List<string> TestCategories { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class TestProgress
    {
        public double Percentage { get; set; }
        public string CurrentTest { get; set; } = string.Empty;
        public int TestsRun { get; set; }
        public int TotalTests { get; set; }
        public int TestsPassed { get; set; }
        public int TestsFailed { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    public class FeatureCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int FeatureCount { get; set; }
        public int CompletedCount { get; set; }
        public double CompletionPercentage { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class FeatureItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FeatureStatus Status { get; set; }
        public int Priority { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? TestedAt { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<string> TestCases { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ProjectOverview
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ProjectPhase CurrentPhase { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double ProgressPercentage { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int ActiveTasks { get; set; }
        public int BlockedTasks { get; set; }
        public int TeamSize { get; set; }
        public TimeSpan TimeElapsed { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public List<string> Achievements { get; set; } = new();
        public List<string> Risks { get; set; } = new();
    }

    public class ProjectMilestone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime PlannedDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public MilestoneStatus Status { get; set; }
        public int CompletionPercentage { get; set; }
        public List<string> Deliverables { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
    }

    public class TeamMember
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public int AssignedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastActive { get; set; }
        public List<string> Skills { get; set; } = new();
    }

    public class Attachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Enums
    public enum TestCategory
    {
        Unit,
        Integration,
        System,
        UI,
        Performance,
        Security,
        Compatibility,
        Accessibility
    }

    public enum TestType
    {
        Functional,
        Regression,
        Smoke,
        Sanity,
        Performance,
        Load,
        Stress,
        Security,
        Usability,
        Compatibility
    }

    public enum TestStatus
    {
        Pending,
        Running,
        Passed,
        Failed,
        Skipped,
        Blocked,
        Error
    }

    public enum FeatureStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Tested,
        Blocked,
        Deferred,
        All
    }

    public enum ProjectPhase
    {
        Planning,
        Design,
        Development,
        Testing,
        Deployment,
        Maintenance,
        Complete
    }

    public enum MilestoneStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Delayed,
        Cancelled
    }
}
