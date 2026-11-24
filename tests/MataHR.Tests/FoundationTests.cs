using Xunit;
using MataHR.Foundation;
using MataHR.Domain;
public sealed class FoundationTests {
 [Fact] public void AdminDoesNotInheritPayroll(){Assert.False(Permissions.Allows(["Admin"],Permissions.PayrollPrepare));Assert.True(Permissions.Allows(["Admin"],Permissions.AccountsManage));Assert.True(Permissions.Allows(["HR","Employee"],Permissions.PayrollPrepare));}
 [Fact] public void RejectsReportingCycles(){var a=Guid.NewGuid();var b=Guid.NewGuid();var links=new Dictionary<Guid,Guid?>{{a,null},{b,a}};Assert.Throws<DomainRuleException>(()=>EmploymentRules.ValidateReporting(a,b,links));EmploymentRules.ValidateReporting(b,a,links);}
 [Fact] public void ContractCannotSkipReview(){Assert.Throws<DomainRuleException>(()=>ContractRules.ValidateTransition(ContractStage.Draft,ContractStage.Signed,true,true,"note",Guid.NewGuid(),null));}
 [Fact] public void ContractRequiresEvidence(){Assert.Throws<DomainRuleException>(()=>ContractRules.ValidateTransition(ContractStage.AwaitingSignature,ContractStage.Signed,true,false,"note",Guid.NewGuid(),null));}
}
