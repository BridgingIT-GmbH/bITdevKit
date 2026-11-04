namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Presentation.Web.Dashboard;

public class DashboardHistoryCursorTests
{
    [Fact]
    public void Apply_TiesAndNewArrivals_PreservesChronologicalSnapshotAcrossBatches()
    {
        var time = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(1, 4).Select(i => new BrokerMessageInfo
        {
            Id = Guid.Parse($"{i:D8}-0000-0000-0000-000000000000"), CreatedDate = time
        }).ToList();
        rows.Add(new() { Id = Guid.Parse("ffffffff-0000-0000-0000-000000000000"), CreatedDate = time.AddMinutes(-1) });
        var firstFilter = new FilterModel { Page = 1, PageSize = 2 };
        DashboardHistoryCursor.Apply(firstFilter, "Id", null, null);
        var first = rows.AsQueryable().ApplyFilter(firstFilter).ApplyOrder(firstFilter).ApplyPage(firstFilter).ToArray();
        var boundary = DashboardHistoryCursor.Encode(first[0].CreatedDate, first[0].Id);
        var cursor = DashboardHistoryCursor.Encode(first[1].CreatedDate, first[1].Id);
        rows.Add(new() { Id = Guid.NewGuid(), CreatedDate = time.AddMinutes(1) });
        var nextFilter = new FilterModel { Page = 1, PageSize = 10 };

        DashboardHistoryCursor.Apply(nextFilter, "Id", cursor, boundary);
        var next = rows.AsQueryable().ApplyFilter(nextFilter).ApplyOrder(nextFilter).ApplyPage(nextFilter).ToArray();

        first.Select(row => row.Id).ShouldBe(rows.Take(4).Reverse().Take(2).Select(row => row.Id));
        next.Length.ShouldBe(3);
        next.Select(row => row.CreatedDate).ShouldBe(new[] { time, time, time.AddMinutes(-1) });
        first.Concat(next).Select(row => row.Id).Distinct().Count().ShouldBe(5);
        next.ShouldNotContain(row => row.CreatedDate > time);
    }
}
