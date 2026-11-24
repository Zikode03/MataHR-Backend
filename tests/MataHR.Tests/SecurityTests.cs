using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
public sealed class SecurityTests{
 [Fact] public async Task AnonymousRequestsCannotReadEmployees(){
 var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db");
 using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{b.UseEnvironment("Testing");b.ConfigureAppConfiguration((_,cfg)=>cfg.AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Default","Data Source="+path}}));});
 using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
 Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/employees")).StatusCode);
 Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/login",new{tenantId=Guid.NewGuid(),email="demo@example.invalid",password="fictional"})).StatusCode);
 client.DefaultRequestHeaders.Add("X-MataHR-Request","1");
 Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/login",new{tenantId=Guid.NewGuid(),email="demo@example.invalid",password="fictional"})).StatusCode);
 }
 [Fact] public void EmploymentDatesAreBounded(){var e=new MataHR.Employee{Started=new DateOnly(2025,1,1),Exited=new DateOnly(2026,1,1)};Assert.False(MataHR.Access.Active(e,new DateOnly(2024,12,31)));Assert.True(MataHR.Access.Active(e,new DateOnly(2025,1,1)));Assert.False(MataHR.Access.Active(e,new DateOnly(2026,1,1)));}
}