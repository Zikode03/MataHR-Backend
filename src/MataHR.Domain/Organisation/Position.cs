using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Organisation;
[Volo.Abp.Auditing.DisableAuditing]
public class Position : WorkforceRow
{
    public Guid DepartmentId { get; private set; }
    public string Name { get; private set; } = "";

    protected Position() { }

    public Position(Guid parentId, string name)
    {
        DepartmentId = parentId;
        Name = DomainGuard.Text(name, "Name", 160);
    }
}
