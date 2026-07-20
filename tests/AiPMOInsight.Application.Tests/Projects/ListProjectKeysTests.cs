using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Projects;
using AiPMOInsight.Domain.Findings;
using Xunit;
using static AiPMOInsight.Application.Tests.HealthScoring.HealthScoringFixtures;

namespace AiPMOInsight.Application.Tests.Projects;

/// <summary>
/// Unit tests for the project-keys enumeration slice. Feeds the L2 project switcher and any other
/// caller that needs to list projects on record. Reads through <see cref="IFindingRepository.DistinctProjectKeysAsync"/>;
/// sorts alphabetically so the dropdown order is stable.
/// </summary>
public class ListProjectKeysTests
{
    private sealed class FakeRepo(IReadOnlyList<string> keys) : IFindingRepository
    {
        public Task AddAsync(Finding f, CancellationToken ct) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Finding> f, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Finding>> GetByProjectKeyAsync(string projectKey, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(Array.Empty<Finding>());
        public Task<IReadOnlyList<Finding>> GetByUploadIdAsync(Guid uploadId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Finding>>(Array.Empty<Finding>());
        public Task<IReadOnlyList<string>> DistinctProjectKeysAsync(CancellationToken ct) =>
            Task.FromResult(keys);
    }

    private static Task<IReadOnlyList<string>> Run(IReadOnlyList<string> keys) =>
        new ListProjectKeys.Handler(new FakeRepo(keys))
            .Handle(new ListProjectKeys.Query(), CancellationToken.None);

    [Fact]
    public async Task Returns_keys_sorted_alphabetically()
    {
        // Ensure the slice — not the caller — imposes stable ordering, since DistinctProjectKeysAsync
        // makes no order guarantee.
        var result = await Run(new[] { "ORB-1003", "ALPHA", "ORB-1001" });

        result.Should().ContainInOrder("ALPHA", "ORB-1001", "ORB-1003");
    }

    [Fact]
    public async Task Empty_store_returns_empty_list()
    {
        var result = await Run(Array.Empty<string>());

        result.Should().BeEmpty();
    }
}
