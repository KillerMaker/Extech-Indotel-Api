using Exatech_Indotel_API.Models.Client;
using FluentValidation;

namespace Exatech_Indotel_API.Utilities.Validators
{
    public class CreateClientValidator : AbstractValidator<CreateClientRequest>
    {
        public CreateClientValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name Is Required");
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email Is Required");
            RuleFor(x => x.NationalIdentificationNumber).NotEmpty().WithMessage("National Identification Number Is Required");
            RuleFor(x => x.PobertyLevel).NotEmpty().WithMessage("Poberty Level Is Required");
        }
    }
}
