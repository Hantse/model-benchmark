using System.Net;
using System.Net.Http.Json;
using Benchmark.Frontend.Api;
using Xunit;
namespace Benchmark.Frontend.Tests;
public sealed class HttpClientTests
{
    private sealed class Transport : HttpMessageHandler
    {
        public List<(string Method,string Path,string? Bearer,string? Body)> Calls {get;}=[];
        public HttpStatusCode Code=HttpStatusCode.OK;
        public string Response="[]";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls.Add((request.Method.Method,request.RequestUri!.PathAndQuery,request.Headers.Authorization?.ToString(),request.Content==null?null:await request.Content.ReadAsStringAsync(ct)));return new(Code){Content=new StringContent(Response,System.Text.Encoding.UTF8,"application/json")};}
    }
    private static WorkItemApiClient Client(Transport t)=>new(new HttpClient(t){BaseAddress=new Uri("http://localhost:5080")});
    [Fact] public async Task Login_posts_credentials_and_reads_access_token(){var t=new Transport{Response="{\"accessToken\":\"returned-token\"}"};Assert.Equal("returned-token",await Client(t).LoginAsync("alice@example.test","Alice!234"));var c=Assert.Single(t.Calls);Assert.Equal("POST",c.Method);Assert.Equal("/api/auth/login",c.Path);Assert.Null(c.Bearer);using var j=System.Text.Json.JsonDocument.Parse(c.Body!);Assert.Equal("alice@example.test",j.RootElement.GetProperty("email").GetString());Assert.Equal("Alice!234",j.RootElement.GetProperty("password").GetString());}
    [Fact] public async Task List_uses_per_request_bearer_and_reads_versions(){var id=Guid.NewGuid();var t=new Transport{Response=$"[{{\"id\":\"{id}\",\"title\":\"A\",\"status\":\"Done\",\"version\":3}}]"};var items=await Client(t).ListAsync("token-a");Assert.Equal(new(id,"A","Done",3),items.Single());Assert.Equal("Bearer token-a",t.Calls.Single().Bearer);Assert.Equal("/api/work-items",t.Calls.Single().Path);}
    [Fact] public async Task Create_and_update_send_status_and_expected_version(){var id=Guid.NewGuid();var t=new Transport{Response=$"{{\"id\":\"{id}\",\"title\":\"A\",\"status\":\"Todo\",\"version\":1}}"};var c=Client(t);await c.AddAsync("token","A","Todo");await c.UpdateAsync("token",id,"B","Done",1);Assert.Equal("POST",t.Calls[0].Method);Assert.Equal("PUT",t.Calls[1].Method);Assert.Equal($"/api/work-items/{id}",t.Calls[1].Path);using var j=System.Text.Json.JsonDocument.Parse(t.Calls[1].Body!);Assert.Equal(1,j.RootElement.GetProperty("expectedVersion").GetInt32());Assert.Equal("Done",j.RootElement.GetProperty("status").GetString());Assert.All(t.Calls,c=>Assert.Equal("Bearer token",c.Bearer));}
    [Fact] public async Task Delete_sends_expected_version_query_and_accepts_204(){var id=Guid.NewGuid();var t=new Transport{Code=HttpStatusCode.NoContent,Response=""};await Client(t).DeleteAsync("token",id,7);var c=Assert.Single(t.Calls);Assert.Equal("DELETE",c.Method);Assert.Equal($"/api/work-items/{id}?expectedVersion=7",c.Path);Assert.Equal("Bearer token",c.Bearer);}
    [Fact] public async Task HTTP_failure_maps_status_for_effects_without_fake_success(){var t=new Transport{Code=HttpStatusCode.Unauthorized,Response="{\"error\":\"expired\"}"};var e=await Assert.ThrowsAsync<ApiFailureException>(()=>Client(t).ListAsync("expired"));Assert.Equal(401,e.Status);}
    [Fact] public async Task Different_sessions_do_not_mutate_shared_default_headers(){var t=new Transport();var http=new HttpClient(t){BaseAddress=new Uri("http://localhost:5080")};var c=new WorkItemApiClient(http);await c.ListAsync("one");await c.ListAsync("two");Assert.Equal("Bearer one",t.Calls[0].Bearer);Assert.Equal("Bearer two",t.Calls[1].Bearer);Assert.Null(http.DefaultRequestHeaders.Authorization);}
}
