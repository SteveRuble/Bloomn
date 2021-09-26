using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Bloomn
{
    internal class OptionsValidator<T> : IValidateOptions<BloomFilterOptions<T>>
    {
        public ValidateOptionsResult Validate(string name, BloomFilterOptions<T> options)
        {
            var failures = new List<string>();
            try
            {
                var _ = options.Dimensions?.Build();
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
            }

            try
            {
                options.Scaling.Validate();
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
            }

            try
            {
                var _ = options.GetHasherFactory();
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
            }

            if (failures.Any())
            {
                return ValidateOptionsResult.Fail(failures);
            }

            return ValidateOptionsResult.Success;
        }
    }
}