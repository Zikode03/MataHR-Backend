using MataHR.Common;
namespace MataHR.Employees;

public readonly record struct EmployeeNumber
{
    public string Value { get; }

    public EmployeeNumber(string value) => Value = DomainGuard.Text(value, "EmployeeNumber", 64);
    public override string ToString() => Value;
}
