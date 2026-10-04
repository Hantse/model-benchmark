using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TaskBoard.Components.Pages;
using TaskBoard.Services;
using Xunit;
namespace TaskBoard.Tests;
public sealed class ComponentTests : BunitContext
{
    private readonly TaskBoardService board = new();
    public ComponentTests() { Services.AddSingleton<ITaskBoardService>(board); }
    private IRenderedComponent<TaskBoardPage> Render() => base.Render<TaskBoardPage>();
    private static void Login(IRenderedComponent<TaskBoardPage> page, string email = "alice@example.test", string password = "Alice!234") { page.Find("[data-testid=email]").Change(email); page.Find("[data-testid=password]").Change(password); page.Find("[data-testid=login]").Click(); }
    [Fact] public void Public_heading_is_preserved() { Assert.Equal("Task Board", Render().Find("h1").TextContent); }
    [Fact] public void Anonymous_view_shows_login_and_hides_tasks() { var p = Render(); Assert.Equal("password", p.Find("[data-testid=password]").GetAttribute("type")); Assert.Empty(p.FindAll("[data-testid=task-title]")); Assert.NotNull(p.Find("[data-testid=login]")); }
    [Fact] public void Invalid_login_displays_error() { var p = Render(); Login(p, password: "wrong"); p.WaitForAssertion(() => Assert.False(string.IsNullOrWhiteSpace(p.Find("[role=alert]").TextContent))); Assert.Null(board.CurrentUser); }
    [Fact] public void Valid_login_allows_task_creation() { var p = Render(); Login(p); p.WaitForElement("[data-testid=task-title]"); p.Find("[data-testid=task-title]").Change("Design UI"); p.Find("[data-testid=add]").Click(); p.WaitForAssertion(() => Assert.Contains("Design UI", p.Find("[data-testid=tasks]").TextContent)); }
    [Fact] public async Task Completion_and_filter_keep_all_tasks() { Assert.True(await board.LoginAsync("alice@example.test", "Alice!234")); await board.AddAsync("Pending"); var p = Render(); var check = p.Find("[data-testid=complete]"); check.Change(true); p.Find("[data-testid=filter]").Change("Active"); p.WaitForAssertion(() => Assert.Empty(p.FindAll("[data-testid=task-row]"))); p.Find("[data-testid=filter]").Change("Completed"); p.WaitForAssertion(() => Assert.Single(p.FindAll("[data-testid=task-row]"))); p.Find("[data-testid=filter]").Change("All"); p.WaitForAssertion(() => Assert.Single(p.FindAll("[data-testid=task-row]"))); }
    [Fact] public async Task Delete_removes_visible_task() { Assert.True(await board.LoginAsync("alice@example.test", "Alice!234")); await board.AddAsync("Delete me"); var p = Render(); p.Find("[data-testid=delete]").Click(); p.WaitForAssertion(() => Assert.Empty(p.FindAll("[data-testid=task-row]"))); Assert.Empty(await board.ListAsync()); }
    [Fact] public void Invalid_title_displays_error_and_preserves_tasks() { var p = Render(); Login(p); p.WaitForElement("[data-testid=task-title]"); p.Find("[data-testid=task-title]").Change(" "); p.Find("[data-testid=add]").Click(); p.WaitForElement("[role=alert]"); Assert.Empty(p.FindAll("[data-testid=task-row]")); }
    [Fact] public async Task Logout_hides_private_state_and_next_user_has_empty_view() { Assert.True(await board.LoginAsync("alice@example.test", "Alice!234")); await board.AddAsync("Secret"); var p = Render(); p.Find("[data-testid=logout]").Click(); p.WaitForElement("[data-testid=login]"); Assert.DoesNotContain("Secret", p.Markup); Login(p, "bob@example.test", "Bob!234"); p.WaitForElement("[data-testid=tasks]"); Assert.Empty(p.FindAll("[data-testid=task-row]")); }
}
