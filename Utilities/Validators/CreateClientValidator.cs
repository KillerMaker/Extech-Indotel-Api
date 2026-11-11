using Exatech_Indotel_API.Models.Client;
using FluentValidation;
using Exatech_Indotel_API.Services.Siuben;

namespace Exatech_Indotel_API.Utilities.Validators
{
    public class CreateClientValidator : AbstractValidator<CreateClientRequest>
    {
        public CreateClientValidator(SiubenLocalities localities)
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name Is Required");
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email Is Required");
            RuleFor(x => x.NationalIdentificationNumber).NotEmpty().WithMessage("National Identification Number Is Required");
            RuleFor(x => x.Locality).Must(localities.IsSupported)
                .WithMessage("Locality is required and must be a supported Siuben locality.");
            RuleFor(x => x.PobertyLevel).NotEmpty().WithMessage("Poberty Level Is Required")
                .When(x => localities.IsSupported(x.Locality) && localities.GetPhase(x.Locality) == SiubenPhase.Phase1);
            When(x => localities.IsSupported(x.Locality) && localities.GetPhase(x.Locality) == SiubenPhase.Phase2, () =>
            {
                RuleFor(x => x.NationalIdentificationNumber).Matches(@"^(?:[0-9]{11}|[0-9]{3}-[0-9]{7}-[0-9])$");
                RuleFor(x => x).Must(x => SiubenPhoneNumber.IsValid(x.PhoneMobile, x.Phone))
                    .WithName("phoneMobile").WithMessage("Phase 2 requires a valid 10-digit beneficiary phone number.");
            });
        }
    }
}
