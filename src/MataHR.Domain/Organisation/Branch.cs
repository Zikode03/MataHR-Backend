using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Organisation;
[Volo.Abp.Auditing.DisableAuditing]
public class Branch : WorkforceRow
{
    public string Name { get; private set; } = "";

    protected Branch() { }

    public Branch(string name)
    {
        Name = DomainGuard.Text(name, "Name", 160);
    }
}
