using MataHR.Persistence.Legacy;
using MataHR.Attendance;
using MataHR.Auditing;
using MataHR.Authentication;
using MataHR.Common;
using MataHR.Documents;
using MataHR.Employees;
using MataHR.Imports;
using MataHR.Leave;
using MataHR.Organisation;
using MataHR.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;

namespace MataHR.Persistence;
public class MataHrDbContext(DbContextOptions<MataHrDbContext> options) : AbpDbContext<MataHrDbContext>(options)
{
    public DbSet<Volo.Abp.Identity.IdentityUser> Users => Set<Volo.Abp.Identity.IdentityUser>();
    public DbSet<Volo.Abp.Identity.IdentityRole> Roles => Set<Volo.Abp.Identity.IdentityRole>();
    public DbSet<PresenceChallenge> PresenceChallenges=>Set<PresenceChallenge>();
    public DbSet<LegacyImportBatch> LegacyImportBatches=>Set<LegacyImportBatch>();
    public DbSet<OrganisationCanvas> OrganisationCanvases=>Set<OrganisationCanvas>();
    public DbSet<Employee> WorkerRows => Set<Employee>();
    public DbSet<LegacyWorkforceAccount> WorkforceUserRows => Set<LegacyWorkforceAccount>();
    public DbSet<Branch> OfficeRows => Set<Branch>();
    public DbSet<Department> TeamRows => Set<Department>();
    public DbSet<Position> JobRows => Set<Position>();
    public DbSet<ChartCard> ChartCardRows => Set<ChartCard>();
    public DbSet<EmploymentChange> EmploymentChangeRows => Set<EmploymentChange>();
    public DbSet<EmploymentContract> WorkContractRows => Set<EmploymentContract>();
    public DbSet<SecureDocument> SecureDocumentRows => Set<SecureDocument>();
    public DbSet<ClockLocation> ClockLocationRows => Set<ClockLocation>();
    public DbSet<WorkSchedule> WorkScheduleRows => Set<WorkSchedule>();
    public DbSet<WorkSession> WorkSessionRows => Set<WorkSession>();
    public DbSet<TimeRequest> TimeRequestRows => Set<TimeRequest>();
    public DbSet<LeavePolicy> AbsencePolicyRows => Set<LeavePolicy>();
    public DbSet<PublicHoliday> PublicHolidayRows => Set<PublicHoliday>();
    public DbSet<LeaveBalanceEntry> LeaveBalanceEntries => Set<LeaveBalanceEntry>();
    public DbSet<LeaveAllocation> LeaveAllocationRows => Set<LeaveAllocation>();
    public DbSet<LeaveRequest> AbsenceRows => Set<LeaveRequest>();
    public DbSet<Compensation> CompensationRows => Set<Compensation>();
    public DbSet<EmployeeBenefit> EmployeeBenefitRows => Set<EmployeeBenefit>();
    public DbSet<PayRun> PayRunRows => Set<PayRun>();
    public DbSet<ReleasedPayslip> ReleasedPayslipRows => Set<ReleasedPayslip>();
    public DbSet<WorkforceAudit> WorkforceAuditRows => Set<WorkforceAudit>();
    public DbSet<PrivacyAcceptance> PrivacyAcceptanceRows => Set<PrivacyAcceptance>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ConfigureIdentity();
        b.ConfigurePermissionManagement();
        b.ConfigureAuditLogging();
        b.Entity<Compensation>().Property(x => x.EffectiveFrom).HasDefaultValue(new DateOnly(1, 1, 1));
        b.Entity<EmployeeBenefit>().Ignore(x => x.Active);
        b.Entity<EmployeeBenefit>().Property<bool>("LegacyActive").HasColumnName("Active").HasDefaultValue(true);
        b.Entity<EmploymentContract>().Ignore(x => x.State);
        b.Entity<LeaveRequest>().Ignore(x => x.WorkingDates);
        // Keep SQL names independent of CLR names so a code rename cannot rename a table.
        var tables = new Dictionary<Type, string>
        {
            [typeof(PresenceChallenge)] = "MataPresenceChallenge",
            [typeof(LegacyImportBatch)] = "MataLegacyImportBatch",
            [typeof(OrganisationCanvas)] = "MataOrganisationCanvas",
            [typeof(Employee)] = "MataEmployee",
            [typeof(LegacyWorkforceAccount)] = "MataWorkforceUser",
            [typeof(Branch)] = "MataBranch",
            [typeof(Department)] = "MataDepartment",
            [typeof(Position)] = "MataPosition",
            [typeof(ChartCard)] = "MataChartCard",
            [typeof(EmploymentChange)] = "MataEmploymentChange",
            [typeof(EmploymentContract)] = "MataEmploymentContract",
            [typeof(SecureDocument)] = "MataSecureDocument",
            [typeof(ClockLocation)] = "MataClockLocation",
            [typeof(WorkSchedule)] = "MataWorkSchedule",
            [typeof(WorkSession)] = "MataWorkSession",
            [typeof(TimeRequest)] = "MataTimeRequest",
            [typeof(LeavePolicy)] = "MataLeavePolicy",
            [typeof(PublicHoliday)] = "MataPublicHoliday",
            [typeof(LeaveBalanceEntry)] = "MataLeaveBalanceEntry",
            [typeof(LeaveAllocation)] = "MataLeaveAllocation",
            [typeof(LeaveRequest)] = "MataLeaveRequest",
            [typeof(Compensation)] = "MataCompensation",
            [typeof(EmployeeBenefit)] = "MataEmployeeBenefit",
            [typeof(PayRun)] = "MataPayRun",
            [typeof(ReleasedPayslip)] = "MataReleasedPayslip",
            [typeof(WorkforceAudit)] = "MataWorkforceAudit",
            [typeof(PrivacyAcceptance)] = "MataPrivacyAcceptance",
        };
        foreach (var (type, tableName) in tables)
        {
            var e = b.Entity(type);
            e.ToTable(tableName);
            e.ConfigureByConvention();
            if (typeof(WorkforceEntity).IsAssignableFrom(type))
                e.Property<string>("ExtraProperties").IsRequired().HasDefaultValue("{}");
            e.Property(nameof(WorkforceRow.TenantId)).IsRequired();
        }

        b.Entity<OrganisationCanvas>().HasIndex(x=>x.TenantId).IsUnique();
        b.Entity<PresenceChallenge>().Property(x=>x.CodeHash).HasMaxLength(64);
        b.Entity<PresenceChallenge>().HasIndex(x=>new{x.TenantId,x.LocationId,x.ExpiresAt});
        b.Entity<WorkSession>().HasIndex(x=>new{x.TenantId,x.EmployeeId,x.PresenceChallengeId}).IsUnique().HasFilter("[PresenceChallengeId] IS NOT NULL");
        b.Entity<Employee>().Property(x => x.Number).HasMaxLength(64);
        b.Entity<Employee>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Employee>().Property(x => x.Email).HasMaxLength(254);
        b.Entity<Employee>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.Entity<Employee>().HasIndex(x => new { x.TenantId, x.ManagerId });
        b.Entity<LegacyWorkforceAccount>().Property(x => x.Email).HasMaxLength(254);
        b.Entity<LegacyWorkforceAccount>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        b.Entity<LegacyWorkforceAccount>().Property(x => x.PasswordHash).HasMaxLength(512);
        b.Entity<LegacyWorkforceAccount>().Property(x => x.Roles).HasMaxLength(100);
        b.Entity<Branch>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Department>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Position>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Branch>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<Department>().HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
        b.Entity<Position>().HasIndex(x => new { x.TenantId, x.DepartmentId, x.Name }).IsUnique();
        foreach (var t in new[]
        {
            typeof(Employee),
            typeof(Branch),
            typeof(Department),
            typeof(Position),
            typeof(ClockLocation),
            typeof(LeavePolicy),
            typeof(WorkSession),
            typeof(SecureDocument),
            typeof(PayRun)
        }

        )
            b.Entity(t).HasAlternateKey(nameof(WorkforceRow.TenantId), nameof(WorkforceRow.Id));
        b.Entity<Department>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Position>().HasOne<Department>().WithMany().HasForeignKey(x => new { x.TenantId, x.DepartmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Employee>().HasOne<Position>().WithMany().HasForeignKey(x => new { x.TenantId, x.PositionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Employee>().HasOne<Employee>().WithMany().HasForeignKey(x => new { x.TenantId, x.ManagerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkSession>().Property(x => x.Status).HasMaxLength(32);
        b.Entity<WorkSession>().HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique().HasFilter("[Status] = 'Open'");
        b.Entity<WorkSession>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.Date });
        b.Entity<LeaveRequest>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.Start });
        b.Entity<PresenceChallenge>().HasAlternateKey(x=>new{x.TenantId,x.Id});
        b.Entity<PresenceChallenge>().HasOne<ClockLocation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LocationId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkSession>().HasOne<PresenceChallenge>().WithMany().HasForeignKey(x=>new{x.TenantId,x.PresenceChallengeId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkSession>().HasOne<ClockLocation>().WithMany().HasForeignKey(x => new { x.TenantId, x.LocationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveRequest>().HasOne<LeavePolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.PolicyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveAllocation>().HasOne<LeavePolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.PolicyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        EmployeeChild<LegacyWorkforceAccount>(b);
        EmployeeChild<ChartCard>(b);
        EmployeeChild<EmploymentChange>(b);
        EmployeeChild<EmploymentContract>(b);
        EmployeeChild<SecureDocument>(b);
        EmployeeChild<WorkSchedule>(b);
        EmployeeChild<WorkSession>(b);
        EmployeeChild<TimeRequest>(b);
        EmployeeChild<LeaveAllocation>(b);
        EmployeeChild<LeaveRequest>(b);
        EmployeeChild<Compensation>(b);
        EmployeeChild<EmployeeBenefit>(b);
        EmployeeChild<ReleasedPayslip>(b);
        b.Entity<TimeRequest>().HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.SessionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReleasedPayslip>().HasOne<PayRun>().WithMany().HasForeignKey(x => new { x.TenantId, x.RunId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReleasedPayslip>().HasOne<SecureDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EmploymentContract>().Property(x => x.Reference).HasMaxLength(100);
        b.Entity<EmploymentContract>().HasIndex(x => new { x.TenantId, x.Reference, x.Version }).IsUnique();
        b.Entity<SecureDocument>().Property(x => x.Name).HasMaxLength(180);
        b.Entity<SecureDocument>().Property(x => x.Content).HasMaxLength(5 * 1024 * 1024);
        b.Entity<SecureDocument>().Property(x => x.Sha256).HasMaxLength(64);
        b.Entity<ChartCard>().HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique();
        b.Entity<WorkSchedule>().HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique();
        b.Entity<Compensation>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveFrom }).IsUnique();
        b.Entity<LeaveAllocation>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<LeaveAllocation>().HasMany(x => x.Entries).WithOne().HasForeignKey(x => new { x.TenantId, x.AllocationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveAllocation>().Navigation(x => x.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Entity<LeavePolicy>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<LeavePolicy>().HasIndex(x => new { x.TenantId, x.Name, x.Version }).IsUnique();
        b.Entity<LeaveAllocation>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.PolicyId, x.Year }).IsUnique();
        b.Entity<PublicHoliday>().HasIndex(x => new { x.TenantId, x.Date }).IsUnique();
        b.Entity<PayRun>().Property(x => x.Period).HasMaxLength(7);
        b.Entity<PayRun>().HasIndex(x => new { x.TenantId, x.Period }).IsUnique();
        b.Entity<ReleasedPayslip>().HasIndex(x => new { x.TenantId, x.RunId, x.EmployeeId }).IsUnique();
        b.Entity<PrivacyAcceptance>().Property(x => x.Version).HasMaxLength(40);
        b.Entity<PrivacyAcceptance>().HasIndex(x => new { x.TenantId, x.AccountId, x.Version }).IsUnique();
        foreach (var e in b.Model.GetEntityTypes().Where(e => typeof(IWorkforceEntity).IsAssignableFrom(e.ClrType)))
            foreach (var p in e.GetProperties().Where(p => p.ClrType == typeof(decimal)))
            {
                p.SetPrecision(18);
                p.SetScale(2);
            }
    }

    static void EmployeeChild<T>(ModelBuilder b)
        where T : class, IWorkforceEntity => b.Entity<T>().HasOne<Employee>().WithMany().HasForeignKey("TenantId", "EmployeeId").HasPrincipalKey(nameof(Employee.TenantId), nameof(Employee.Id)).OnDelete(DeleteBehavior.Restrict);
}

public class MataHrDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MataHrDbContext>
{
    public MataHrDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<MataHrDbContext>().UseSqlServer("Server=localhost,14336;Database=MataHR_Development;Integrated Security=True;TrustServerCertificate=True").Options);
}
