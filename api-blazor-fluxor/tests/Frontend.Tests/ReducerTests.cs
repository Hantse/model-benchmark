using System.Collections.Immutable;
using Benchmark.Frontend.Api;
using Benchmark.Frontend.Store;
using Xunit;
namespace Benchmark.Frontend.Tests;
public sealed class ReducerTests
{
    private static WorkItemView Item()=>new(Guid.NewGuid(),"Private","Todo",1);
    private static BoardState Signed()=>new("token",4,[Item()],"All",false,null,null);
    [Fact] public void Initial_state_is_anonymous_and_empty(){var s=new BoardState();Assert.Null(s.AccessToken);Assert.Empty(s.Items);Assert.Equal("All",s.Filter);Assert.False(s.Pending);}
    [Fact] public void Login_request_replaces_previous_session_with_pending_state(){var before=Signed();var id=Guid.NewGuid();var after=Reducers.Login(before,new(id,"alice@example.test","Alice!234"));Assert.Equal(before.SessionVersion+1,after.SessionVersion);Assert.Null(after.AccessToken);Assert.Empty(after.Items);Assert.True(after.Pending);Assert.Equal(id,after.ActiveRequest);Assert.Single(before.Items);}
    [Fact] public void Matching_login_success_sets_token_and_finishes_request(){var id=Guid.NewGuid();var s=new BoardState(null,5,[],"All",true,id,null);var after=Reducers.LoginSuccess(s,new(id,5,"signed"));Assert.Equal("signed",after.AccessToken);Assert.False(after.Pending);Assert.Null(after.ActiveRequest);}
    [Fact] public void Late_login_success_cannot_resurrect_logged_out_session(){var s=new BoardState(null,8,[],"All",false,null,null);Assert.Equal(s,Reducers.LoginSuccess(s,new(Guid.NewGuid(),7,"old-token")));}
    [Fact] public void Mutation_requests_mark_pending_without_optimistic_data_change(){var s=Signed();var id=Guid.NewGuid();var after=Reducers.Update(s,new(id,s.Items[0].Id,"New","Done",1));Assert.True(after.Pending);Assert.Equal(id,after.ActiveRequest);Assert.Equal(s.Items,after.Items);Assert.Equal("Private",after.Items[0].Title);}
    [Fact] public void Loaded_result_replaces_items_and_clears_pending(){var id=Guid.NewGuid();var s=Signed() with{Pending=true,ActiveRequest=id};var items=ImmutableArray.Create(Item() with{Status="Done"});var after=Reducers.Loaded(s,new(id,s.SessionVersion,items));Assert.Equal(items,after.Items);Assert.False(after.Pending);Assert.Null(after.ActiveRequest);}
    [Fact] public void Late_items_cannot_resurrect_data_after_logout(){var s=new BoardState(null,8,[],"All",false,null,null);Assert.Equal(s,Reducers.Loaded(s,new(Guid.NewGuid(),7,[Item()])));}
    [Fact] public void Filter_keeps_original_items_immutable(){var s=Signed();var after=Reducers.Filter(s,new("Done"));Assert.Equal("Done",after.Filter);Assert.Equal(s.Items,after.Items);Assert.Equal("All",s.Filter);}
    [Fact] public void Conflict_error_preserves_data_and_finishes_request(){var id=Guid.NewGuid();var s=Signed() with{Pending=true,ActiveRequest=id};var after=Reducers.Failed(s,new(id,s.SessionVersion,409,"Reload item"));Assert.Equal(s.Items,after.Items);Assert.Equal("token",after.AccessToken);Assert.Equal("Reload item",after.Error);Assert.False(after.Pending);}
    [Fact] public void Unauthorized_response_clears_token_and_private_items(){var id=Guid.NewGuid();var s=Signed() with{Pending=true,ActiveRequest=id};var after=Reducers.Failed(s,new(id,s.SessionVersion,401,"Expired"));Assert.Null(after.AccessToken);Assert.Empty(after.Items);Assert.True(after.SessionVersion>s.SessionVersion);Assert.False(after.Pending);}
    [Fact] public void Logout_invalidates_pending_session_and_removes_private_data(){var s=Signed() with{Pending=true,ActiveRequest=Guid.NewGuid(),Error="old"};var after=Reducers.Logout(s,new());Assert.Null(after.AccessToken);Assert.Empty(after.Items);Assert.False(after.Pending);Assert.Null(after.ActiveRequest);Assert.Null(after.Error);Assert.Equal(s.SessionVersion+1,after.SessionVersion);}
    [Fact] public void Duplicate_pending_request_does_not_replace_active_operation(){var s=Signed() with{Pending=true,ActiveRequest=Guid.NewGuid()};Assert.Equal(s,Reducers.Add(s,new(Guid.NewGuid(),"Duplicate","Todo")));}
}
