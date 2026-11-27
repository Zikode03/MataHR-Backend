using MataHR.Common;
using System.Text.Json.Serialization;

namespace MataHR.Payroll;
[Volo.Abp.Auditing.DisableAuditing]
public class ReleasedPayslip : WorkforceRow
{
    public Guid EmployeeId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid DocumentId { get; private set; }
    public bool Released { get; private set; }

    protected ReleasedPayslip() { }

    public ReleasedPayslip(Guid employeeId, Guid runId, Guid documentId)
    {
        EmployeeId = employeeId;
        RunId = runId;
        DocumentId = documentId;
    }

    // Release only moves forward. The application service validates the approved run before calling this.
    public void Release() => Released = true;
}
