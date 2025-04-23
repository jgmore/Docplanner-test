using FluentValidation;
using Docplanner.Common.DTOs;

namespace Docplanner.Application.Validators;

public class BookingRequestValidator : AbstractValidator<BookingRequestDto>
{
    public BookingRequestValidator()
    {
        RuleFor(x => x.Start)
            .NotEmpty().WithMessage("Start time is required");

        RuleFor(x => x.End)
            .NotEmpty().WithMessage("End time is required");

        RuleFor(x => x.End).GreaterThan(x => x.Start)
            .WithMessage("End time must be after Start time");

        RuleFor(x => x.FacilityId)
            .NotEmpty().WithMessage("FacilityId is required");

        RuleFor(x => x.Patient).NotNull().WithMessage("Patient information is required");

        When(x => x.Patient != null, () => {
            RuleFor(x => x.Patient.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("A valid email address is required");

            RuleFor(x => x.Patient.Name)
                .NotEmpty().WithMessage("First name is required");

            RuleFor(x => x.Patient.SecondName)
                .NotEmpty().WithMessage("Last name is required");

            RuleFor(x => x.Patient.Phone)
                .NotEmpty().WithMessage("Phone number is required");
        });
    }
}
