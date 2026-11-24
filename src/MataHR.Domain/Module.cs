using Volo.Abp.Modularity;
using Volo.Abp.Domain;
using MataHR.Foundation;
namespace MataHR.Domain;
[DependsOn(typeof(MataHrDomainSharedModule),typeof(AbpDddDomainModule))]
public sealed class MataHrDomainModule:AbpModule {}
public static class EmploymentRules {
 public static void ValidateReporting(Guid employee,Guid? manager,IReadOnlyDictionary<Guid,Guid?> links){
 var seen=new HashSet<Guid>{employee};while(manager is Guid id){if(!seen.Add(id))throw new DomainRuleException("Circular reporting relationship.");if(!links.TryGetValue(id,out manager))throw new DomainRuleException("Reporting manager does not exist in this organisation.");}
 }
 public static bool Active(DateOnly started,DateOnly? exited,DateOnly on)=>started<=on&&(exited is null||exited>on);
}
public enum ContractStage { Draft, InReview, Approved, AwaitingSignature, Signed }
public static class ContractRules {
 public static void ValidateTransition(ContractStage current,ContractStage next,bool attached,bool verifiedSignedCopy,string? note,Guid actor,Guid? previousReviewer){
 if((current,next) is not ((ContractStage.Draft,ContractStage.InReview) or (ContractStage.InReview,ContractStage.Approved) or (ContractStage.Approved,ContractStage.AwaitingSignature) or (ContractStage.AwaitingSignature,ContractStage.Signed)))throw new DomainRuleException("Invalid contract transition.");
 if(!attached)throw new DomainRuleException("Contract document required.");
 if(next==ContractStage.Approved&&(string.IsNullOrWhiteSpace(note)||actor==previousReviewer))throw new DomainRuleException("Independent approval and review note required.");
 if(next==ContractStage.Signed&&!verifiedSignedCopy)throw new DomainRuleException("Verified signed evidence required.");
 }
}
