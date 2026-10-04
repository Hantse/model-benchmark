using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace TaskApi.Tests;
public sealed class AdminTests
{
    [Fact] public async Task Anonymous_admin_route_returns_401() { using var api = new ApiFixture(); using var c = api.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/admin/summary")).StatusCode); }
    [Fact] public async Task User_admin_route_returns_403() { using var api = new ApiFixture(); using var c = await api.Login(); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/summary")).StatusCode); }
    [Fact] public async Task Admin_summary_counts_all_users_tasks_but_admin_list_stays_private() { using var api = new ApiFixture(); using var alice = await api.Login(); await ApiFixture.Create(alice); using var bob = await api.Login("bob@example.test", "Bob!234"); await ApiFixture.Create(bob); using var admin = await api.Login("admin@example.test", "Admin!234"); Assert.Empty((await admin.GetFromJsonAsync<TaskApi.TaskDto[]>("/api/tasks"))!); var summary = await admin.GetFromJsonAsync<Summary>("/api/admin/summary"); Assert.Equal(2, summary!.TotalTasks); Assert.Equal(3, summary.TotalUsers); }
    private sealed record Summary(int TotalUsers, int TotalTasks);
}
