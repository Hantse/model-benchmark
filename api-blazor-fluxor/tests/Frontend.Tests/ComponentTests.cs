using Bunit;
using Benchmark.Frontend.Api;
using Benchmark.Frontend.Components.Pages;
using Benchmark.Frontend.Store;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Benchmark.Frontend.Tests;
public sealed class ComponentTests : BunitContext
{
    private readonly FakeApi api=new();
    public ComponentTests(){Services.AddSingleton<IWorkItemApi>(api);Services.AddFluxor(o=>o.ScanAssemblies(typeof(BoardState).Assembly).WithLifetime(StoreLifetime.Scoped));Services.GetRequiredService<IStore>().InitializeAsync().GetAwaiter().GetResult();}
    private IRenderedComponent<WorkBoard> Page()=>Render<WorkBoard>();
    private static void Login(IRenderedComponent<WorkBoard> p,string password="Alice!234"){p.Find("[data-testid=email]").Change("alice@example.test");p.Find("[data-testid=password]").Change(password);p.Find("[data-testid=login]").Click();}
    [Fact] public void Public_heading_is_preserved(){Assert.Equal("Work Board",Page().Find("h1").TextContent);}
    [Fact] public void Anonymous_view_has_accessible_login_and_no_editor(){var p=Page();Assert.Equal("password",p.Find("[data-testid=password]").GetAttribute("type"));Assert.Contains("Email",p.Markup);Assert.Empty(p.FindAll("[data-testid=title]"));}
    [Fact] public void Login_then_create_dispatches_effect_and_renders_item(){var p=Page();Login(p);p.WaitForElement("[data-testid=title]");p.Find("[data-testid=title]").Change("New item");p.Find("[data-testid=create-status]").Change("InProgress");p.Find("[data-testid=add]").Click();p.WaitForAssertion(()=>Assert.Contains("New item",p.Find("[data-testid=items]").TextContent));Assert.Contains(api.Calls,c=>c.Method=="POST"&&c.Status=="InProgress");}
    [Fact] public void Invalid_login_displays_alert(){var p=Page();Login(p,"wrong");p.WaitForAssertion(()=>Assert.False(string.IsNullOrWhiteSpace(p.Find("[role=alert]").TextContent)));Assert.Empty(p.FindAll("[data-testid=title]"));}
    [Fact] public void Pending_login_disables_submit_until_completed(){api.LoginGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var p=Page();Login(p);p.WaitForAssertion(()=>Assert.True(p.Find("[data-testid=login]").HasAttribute("disabled")));Services.GetRequiredService<IDispatcher>().Dispatch(new LogoutRequested());api.LoginGate.SetResult("late-token");}
    [Fact] public void Filter_changes_visible_subset_and_restores_all(){api.Items=[new(Guid.NewGuid(),"Todo item","Todo",1),new(Guid.NewGuid(),"Done item","Done",2)];var p=Page();Login(p);p.WaitForAssertion(()=>Assert.Equal(2,p.FindAll("[data-testid=item-row]").Count));p.Find("[data-testid=filter]").Change("Done");p.WaitForAssertion(()=>Assert.Single(p.FindAll("[data-testid=item-row]")));Assert.Contains("Done item",p.Find("[data-testid=items]").TextContent);p.Find("[data-testid=filter]").Change("All");p.WaitForAssertion(()=>Assert.Equal(2,p.FindAll("[data-testid=item-row]").Count));}
    [Fact] public void Save_status_uses_current_server_version(){var item=new WorkItemView(Guid.NewGuid(),"Original","Todo",5);api.Items=[item];var p=Page();Login(p);p.WaitForElement("[data-testid=edit-status]");p.Find("[data-testid=edit-status]").Change("Done");p.Find("[data-testid=save]").Click();p.WaitForAssertion(()=>Assert.Contains(api.Calls,c=>c.Method=="PUT"&&c.Status=="Done"&&c.ExpectedVersion==5));p.WaitForAssertion(()=>Assert.Contains("Done",p.Find("[data-testid=items]").TextContent));}
    [Fact] public void Delete_uses_current_version_and_removes_row(){var item=new WorkItemView(Guid.NewGuid(),"Remove me","Todo",3);api.Items=[item];var p=Page();Login(p);p.WaitForElement("[data-testid=delete]");p.Find("[data-testid=delete]").Click();p.WaitForAssertion(()=>Assert.Empty(p.FindAll("[data-testid=item-row]")));Assert.Contains(api.Calls,c=>c.Method=="DELETE"&&c.ExpectedVersion==3);}
    [Fact] public void Logout_removes_private_markup_and_returns_to_login(){api.Items=[new(Guid.NewGuid(),"Private item","Todo",1)];var p=Page();Login(p);p.WaitForElement("[data-testid=logout]");p.WaitForAssertion(()=>Assert.Contains("Private item",p.Markup));p.Find("[data-testid=logout]").Click();p.WaitForElement("[data-testid=login]");Assert.DoesNotContain("Private item",p.Markup);Assert.Empty(Services.GetRequiredService<IState<BoardState>>().Value.Items);}
}
