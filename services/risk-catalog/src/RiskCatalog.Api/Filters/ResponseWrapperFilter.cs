using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Api.Filters;

public class ResponseWrapperFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null, StatusCode: >= 200 and < 300 } objectResult)
        {
            var dtoType = typeof(ResponseBaseDTO<>).MakeGenericType(objectResult.Value.GetType());
            var wrapper = Activator.CreateInstance(dtoType);

            dtoType.GetProperty("StatusCode")?.SetValue(wrapper, objectResult.StatusCode ?? 200);
            dtoType.GetProperty("Success")?.SetValue(wrapper, true);
            dtoType.GetProperty("Data")?.SetValue(wrapper, objectResult.Value);
            context.Result = new ObjectResult(wrapper)
            {
                StatusCode = objectResult.StatusCode
            };
        }

        await next();
    }
}