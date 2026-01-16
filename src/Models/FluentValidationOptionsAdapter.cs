using FluentValidation;
using Microsoft.Extensions.Options;

namespace WageringStatsApi.Models;

public class FluentValidationOptionsAdapter<T> : IValidateOptions<T> where T : class
{
    private readonly IValidator<T> _validator;

    public FluentValidationOptionsAdapter(IValidator<T> validator)
    {
        _validator = validator;
    }

    public ValidateOptionsResult Validate(string? name, T options)
    {
        var result = _validator.Validate(options);

        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = result.Errors.Select(e => e.ErrorMessage).ToList();

        
        return ValidateOptionsResult.Fail(errors);
    }
}
