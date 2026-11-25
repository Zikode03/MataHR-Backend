using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
namespace MataHR;
public abstract class TenantRow { public Guid Id {get;set;}=Guid.NewGuid(); public Guid TenantId {get;set;} }
public sealed class Tenant { public Guid Id {get;set;} public string Name {get;set;}=""; }
public sealed class Account:TenantRow { public string Email {get;set;}=""; [JsonIgnore] public string PasswordHash {get;set;}=""; public string Roles {get;set;}="Employee"; public Guid? EmployeeId {get;set;} public bool Active {get;set;}=true; }
public sealed class Employee:TenantRow { public string Number {get;set;}=""; public string Name {get;set;}=""; public string Email {get;set;}=""; public string Department {get;set;}=""; public string Position {get;set;}=""; public string ContractType {get;set;}=""; public DateOnly Started {get;set;} public DateOnly? Exited {get;set;} public Guid? ManagerId {get;set;} }
public sealed class Department:TenantRow { public string Name {get;set;}=""; public Guid? BranchId {get;set;} }
public sealed class Branch:TenantRow { public string Name {get;set;}=""; }
public sealed class Position:TenantRow { public Guid DepartmentId {get;set;} public string Name {get;set;}=""; }
public sealed class Assignment:TenantRow { public Guid EmployeeId {get;set;} public Guid PositionId {get;set;} public Guid? ManagerId {get;set;} }
public sealed class Contract:TenantRow { public Guid EmployeeId {get;set;} public string Reference {get;set;}=""; public DateOnly Start {get;set;} public DateOnly? End {get;set;} public string Status {get;set;}="Draft"; }
public sealed class EmploymentEvent:TenantRow { public Guid EmployeeId {get;set;} public string Description {get;set;}=""; public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow; }
public sealed class WorkLocation:TenantRow { public string Name {get;set;}=""; public string Kind {get;set;}=""; public bool Active {get;set;}=true; }
public sealed class Schedule:TenantRow { public Guid EmployeeId {get;set;} public int DaysMask {get;set;}=62; public TimeOnly Start {get;set;} public TimeOnly End {get;set;} public int Grace {get;set;} }
public sealed class Holiday:TenantRow { public DateOnly Date {get;set;} public string Name {get;set;}=""; }
public sealed class Session:TenantRow { public Guid EmployeeId {get;set;} public Guid LocationId {get;set;} public DateOnly Date {get;set;} public DateTimeOffset Start {get;set;} public DateTimeOffset? End {get;set;} public string Status {get;set;}="Open"; public string Evidence {get;set;}="Unverified"; }
public sealed class AttendanceRequest:TenantRow { public Guid EmployeeId {get;set;} public Guid? SessionId {get;set;} public string Kind {get;set;}="Correction"; public DateOnly Date {get;set;} public DateTimeOffset Start {get;set;} public DateTimeOffset End {get;set;} public string Reason {get;set;}=""; public string Status {get;set;}="Pending"; public string ReviewNote {get;set;}=""; }
public sealed class LeavePolicy:TenantRow { public string Name {get;set;}=""; public bool Paid {get;set;} public bool Limited {get;set;}=true; public decimal AnnualDays {get;set;} public bool MonthlyAccrual {get;set;} public decimal CarryLimit {get;set;} public int NoticeDays {get;set;} public int EvidenceAfter {get;set;} public bool RequiresHr {get;set;} public bool Active {get;set;}=true; }
public sealed class Entitlement:TenantRow { public Guid EmployeeId {get;set;} public Guid PolicyId {get;set;} public int Year {get;set;} public decimal Allocation {get;set;} public decimal Carry {get;set;} public decimal Adjustment {get;set;} }
public sealed class LeaveRequest:TenantRow { public Guid EmployeeId {get;set;} public Guid PolicyId {get;set;} public DateOnly Start {get;set;} public DateOnly End {get;set;} public int Days {get;set;} public bool Paid {get;set;} public bool RequiresHr {get;set;} public string Reason {get;set;}=""; public string EvidenceReference {get;set;}=""; public string Status {get;set;}="Pending"; public string Stage {get;set;}="Manager"; }
public sealed class Salary:TenantRow { public Guid EmployeeId {get;set;} public decimal Basic {get;set;} public decimal Allowance {get;set;} public decimal Deduction {get;set;} }
public sealed class Benefit:TenantRow { public Guid EmployeeId {get;set;} public string Name {get;set;}=""; public decimal Employer {get;set;} public decimal EmployeeCost {get;set;} public bool Active {get;set;}=true; }
public sealed class PayrollRun:TenantRow { public string Period {get;set;}=""; public string Status {get;set;}="Draft"; public Guid? ReviewedBy {get;set;} public Guid? ApprovedBy {get;set;} public string Snapshot {get;set;}="[]"; }
public sealed class PrivacyAck:TenantRow { public Guid AccountId {get;set;} public string Version {get;set;}=""; public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow; }
public sealed class Audit:TenantRow { public Guid ActorId {get;set;} public string Action {get;set;}=""; public Guid TargetId {get;set;} public string Note {get;set;}=""; public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow; }
public sealed class Integration:TenantRow { public string Kind {get;set;}=""; public string Mode {get;set;}="internal"; public string Provider {get;set;}=""; public string Reference {get;set;}=""; }
public sealed class Db(DbContextOptions<Db> options):DbContext(options) {
 public DbSet<Tenant> Tenants=>Set<Tenant>(); public DbSet<Account> Accounts=>Set<Account>();
 public DbSet<Employee> Employees=>Set<Employee>(); public DbSet<Department> Departments=>Set<Department>(); public DbSet<Branch> Branches=>Set<Branch>(); public DbSet<Position> Positions=>Set<Position>(); public DbSet<Assignment> Assignments=>Set<Assignment>(); public DbSet<Contract> Contracts=>Set<Contract>(); public DbSet<EmploymentEvent> EmploymentEvents=>Set<EmploymentEvent>();
 public DbSet<WorkLocation> Locations=>Set<WorkLocation>(); public DbSet<Schedule> Schedules=>Set<Schedule>(); public DbSet<Holiday> Holidays=>Set<Holiday>(); public DbSet<Session> Sessions=>Set<Session>(); public DbSet<AttendanceRequest> AttendanceRequests=>Set<AttendanceRequest>();
 public DbSet<LeavePolicy> Policies=>Set<LeavePolicy>(); public DbSet<Entitlement> Entitlements=>Set<Entitlement>(); public DbSet<LeaveRequest> Leave=>Set<LeaveRequest>();
 public DbSet<Salary> Salaries=>Set<Salary>(); public DbSet<Benefit> Benefits=>Set<Benefit>(); public DbSet<PayrollRun> Runs=>Set<PayrollRun>(); public DbSet<PrivacyAck> PrivacyAcks=>Set<PrivacyAck>(); public DbSet<Audit> Audits=>Set<Audit>(); public DbSet<Integration> Integrations=>Set<Integration>();
 protected override void OnModelCreating(ModelBuilder b){
 b.Entity<Account>().Property(x=>x.Email).HasMaxLength(254); b.Entity<Employee>().Property(x=>x.Number).HasMaxLength(64);
 b.Entity<Branch>().Property(x=>x.Name).HasMaxLength(160); b.Entity<Department>().Property(x=>x.Name).HasMaxLength(160); b.Entity<Position>().Property(x=>x.Name).HasMaxLength(160);
 b.Entity<Session>().Property(x=>x.Status).HasMaxLength(32); b.Entity<PayrollRun>().Property(x=>x.Period).HasMaxLength(7); b.Entity<Integration>().Property(x=>x.Kind).HasMaxLength(32);
 foreach(var entity in b.Model.GetEntityTypes())foreach(var prop in entity.GetProperties().Where(p=>p.ClrType==typeof(decimal))) {prop.SetPrecision(18);prop.SetScale(4);}

 b.Entity<Branch>().HasIndex(x=>new{x.TenantId,x.Name}).IsUnique(); b.Entity<Position>().HasIndex(x=>new{x.TenantId,x.DepartmentId,x.Name}).IsUnique(); b.Entity<Assignment>().HasIndex(x=>new{x.TenantId,x.EmployeeId}).IsUnique();
 b.Entity<Employee>().HasAlternateKey(x=>new{x.TenantId,x.Id}); b.Entity<Branch>().HasAlternateKey(x=>new{x.TenantId,x.Id}); b.Entity<Department>().HasAlternateKey(x=>new{x.TenantId,x.Id}); b.Entity<Position>().HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Entity<Department>().HasOne<Branch>().WithMany().HasForeignKey(x=>new{x.TenantId,x.BranchId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
 b.Entity<Position>().HasOne<Department>().WithMany().HasForeignKey(x=>new{x.TenantId,x.DepartmentId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
 b.Entity<Assignment>().HasOne<Employee>().WithMany().HasForeignKey(x=>new{x.TenantId,x.EmployeeId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
 b.Entity<Assignment>().HasOne<Position>().WithMany().HasForeignKey(x=>new{x.TenantId,x.PositionId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
 b.Entity<Assignment>().HasOne<Employee>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ManagerId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
 b.Entity<Account>().HasIndex(x=>new{x.TenantId,x.Email}).IsUnique(); b.Entity<Employee>().HasIndex(x=>new{x.TenantId,x.Number}).IsUnique();
 b.Entity<Department>().HasIndex(x=>new{x.TenantId,x.Name}).IsUnique(); b.Entity<Schedule>().HasIndex(x=>new{x.TenantId,x.EmployeeId}).IsUnique(); b.Entity<Holiday>().HasIndex(x=>new{x.TenantId,x.Date}).IsUnique(); b.Entity<Entitlement>().HasIndex(x=>new{x.TenantId,x.EmployeeId,x.PolicyId,x.Year}).IsUnique();
 b.Entity<Session>().HasIndex(x=>new{x.TenantId,x.EmployeeId}).IsUnique().HasFilter("[Status] = 'Open'");
 b.Entity<Salary>().HasIndex(x=>new{x.TenantId,x.EmployeeId}).IsUnique(); b.Entity<PayrollRun>().HasIndex(x=>new{x.TenantId,x.Period}).IsUnique(); b.Entity<Integration>().HasIndex(x=>new{x.TenantId,x.Kind}).IsUnique();
 }
}
