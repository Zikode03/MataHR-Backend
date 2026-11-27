using MataHR.Common;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace MataHR.HttpApi.Host;

// Keep client-facing status codes consistent when an ABP authorization, concurrency or business rule rejects a request.
public class WorkforceStatusCodeFinder(IOptions<AbpExceptionHttpStatusCodeOptions> options) : DefaultHttpExceptionStatusCodeFinder(options)
{
    public override HttpStatusCode GetStatusCode(HttpContext context, Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sql && sql.Number is 1205 or 2601 or 2627 or 547)
                return HttpStatusCode.Conflict;
        if (exception is UserFriendlyException or WorkforceRuleException)
            return HttpStatusCode.BadRequest;
        return base.GetStatusCode(context, exception);
    }
}
