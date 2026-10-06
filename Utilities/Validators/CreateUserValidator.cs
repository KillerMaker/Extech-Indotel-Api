using Exatech_Indotel_API.Models.User;
using FluentValidation;

namespace Exatech_Indotel_API.Utilities.Validators
{
    public class CreateUserValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name Is Required");
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email Is Required");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Password Is Required");
            RuleFor(x => x.Password).MinimumLength(6).WithMessage("Password Must Be At Least 6 Characters");
        }
    }
}
