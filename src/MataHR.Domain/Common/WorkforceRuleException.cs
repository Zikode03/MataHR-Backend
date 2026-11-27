using Volo.Abp;

namespace MataHR.Common;

public class WorkforceRuleException(string code, string message) : UserFriendlyException(message, "MataHR:" + code)
{
}
