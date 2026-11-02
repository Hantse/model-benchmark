using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Benchmark.Api;
using Benchmark.Application;
using Benchmark.Domain;
using Benchmark.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
var builder = WebApplication.CreateBuilder(args);
var key = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
var accounts = new Dictionary<string,(string Password,string Role)>
{
    ["alice@example.test"]=("Alice!234","User"),
    ["bob@example.test"]=("Bob!234","User"),
    ["admin@example.test"]=("Admin!234","Admin")
};
builder.Services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options=>
{
    options.TokenValidationParameters=new TokenValidationParameters
    {
        ValidateIssuerSigningKey=true,
        IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuer=true,
        ValidIssuer=issuer,
        ValidateAudience=true,
        ValidAudience=audience,
        ValidateLifetime=true,
        ClockSkew=TimeSpan.Zero,
        NameClaimType=JwtRegisteredClaimNames.Sub
    };
});
builder.Services.AddAuthorization();
var app=builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health",()=>Results.Ok(new{status="ok"}));
app.MapPost("/api/auth/login",(LoginInput input)=>
{
    if(string.IsNullOrWhiteSpace(input.Email)||string.IsNullOrWhiteSpace(input.Password))return Results.BadRequest();
    if(!accounts.TryGetValue(input.Email.Trim(),out var account)||account.Password!=input.Password)return Results.Unauthorized();
    var token=new JwtSecurityToken(issuer,audience,[new Claim(JwtRegisteredClaimNames.Sub,input.Email.Trim()),new Claim(ClaimTypes.Role,account.Role)],DateTime.UtcNow,DateTime.UtcNow.AddMinutes(15),new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),SecurityAlgorithms.HmacSha256));
    return Results.Ok(new LoginResult(new JwtSecurityTokenHandler().WriteToken(token)));
});
app.MapGet("/api/work-items",async(WorkItemService service,ClaimsPrincipal user,WorkStatus? status,CancellationToken ct)=>
{
    if(user.Identity?.IsAuthenticated!=true)return Results.Unauthorized();
    if(status is not null&&!Enum.IsDefined(status.Value))return Results.BadRequest();
    return Results.Ok(await service.ListAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??string.Empty,status,ct));
}).RequireAuthorization();
app.MapPost("/api/work-items",async(WorkItemService service,ClaimsPrincipal user,CreateInput input,CancellationToken ct)=>
{
    if(user.Identity?.IsAuthenticated!=true)return Results.Unauthorized();
    if(!Enum.TryParse<WorkStatus>(input.Status,true,out var status))return Results.BadRequest();
    try{return Results.Created($"/api/work-items/{{id}}",await service.AddAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??string.Empty,input.Title,status,ct));}catch(ArgumentException){return Results.BadRequest();}
}).RequireAuthorization();
app.MapPut("/api/work-items/{id:guid}",async(Guid id,WorkItemService service,ClaimsPrincipal user,UpdateInput input,CancellationToken ct)=>
{
    if(user.Identity?.IsAuthenticated!=true)return Results.Unauthorized();
    if(!Enum.TryParse<WorkStatus>(input.Status,true,out var status))return Results.BadRequest();
    try{return Results.Ok(await service.UpdateAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??string.Empty,id,input.Title,status,input.ExpectedVersion,ct));}catch(ArgumentException){return Results.BadRequest();}catch(ItemNotFoundException){return Results.NotFound();}catch(VersionConflictException){return Results.Conflict();}
}).RequireAuthorization();
app.MapDelete("/api/work-items/{id:guid}",async(Guid id,WorkItemService service,ClaimsPrincipal user,int? expectedVersion,CancellationToken ct)=>
{
    if(user.Identity?.IsAuthenticated!=true)return Results.Unauthorized();
    if(expectedVersion is not{} version)return Results.BadRequest();
    try{await service.DeleteAsync(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??string.Empty,id,version,ct);return Results.NoContent();}catch(ArgumentException){return Results.BadRequest();}catch(ItemNotFoundException){return Results.NotFound();}catch(VersionConflictException){return Results.Conflict();}
}).RequireAuthorization();
app.Run();
public partial class Program { }
