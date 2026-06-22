using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Filters
{
    public class ValidationFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var errors = new List<string>();

            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument == null)
                    continue;

                var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

                var validator = context.HttpContext.RequestServices.GetService(validatorType);

                if (validator == null)
                    continue;

                var validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());

                var validationContext = Activator.CreateInstance(
                    validationContextType,
                    argument) as IValidationContext;

                if (validationContext == null)
                    continue;

                var result = await ((IValidator)validator).ValidateAsync(validationContext);

                if (!result.IsValid)
                {
                    errors.AddRange(
                        result.Errors
                            .Where(x => x != null)
                            .Select(x => x.ErrorMessage)
                            .Distinct());
                }
            }

            if (errors.Any())
            {
                context.Result = new BadRequestObjectResult(
                    ApiResult.Failure(
                        "اطلاعات ارسالی معتبر نیست.",
                        errors));

                return;
            }

            await next();
        }
    }
}