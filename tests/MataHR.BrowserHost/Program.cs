using System.Text.Json;
using MataHR.IntegrationTests;

if (args.Length != 1)
    throw new ArgumentException("Supply the browser state-file path.");
var state = Path.GetFullPath(args[0]);
var fixture = new MataHrApiFixture
{
    BrowserPort = 7143
};
try
{
    // Use a disposable test database while the browser drives the real HTTP endpoints.
    await fixture.InitializeAsync();
    Directory.CreateDirectory(Path.GetDirectoryName(state)!);
    await File.WriteAllTextAsync(state, JsonSerializer.Serialize(new { tenantId = fixture.TenantA, password = MataHrApiFixture.Password, api = "https://localhost:7143" }));
    Console.WriteLine("Disposable browser-test API is ready on loopback port 7143.");
    while (!File.Exists(state + ".stop"))
        await Task.Delay(250);
}
finally
{
    await fixture.DisposeAsync();
    if (File.Exists(state))
        File.Delete(state);
    if (File.Exists(state + ".stop"))
        File.Delete(state + ".stop");
}
