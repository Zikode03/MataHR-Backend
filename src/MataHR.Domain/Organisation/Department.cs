using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Organisation;
[Volo.Abp.Auditing.DisableAuditing]
public class Department : WorkforceRow
{
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = "";

    protected Department() { }

    public Department(Guid parentId, string name)
    {
        BranchId = parentId;
        Name = DomainGuard.Text(name, "Name", 160);
    }
}
