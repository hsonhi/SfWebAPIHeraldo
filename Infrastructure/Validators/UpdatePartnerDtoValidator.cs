using FluentValidation;
using SfWebAPIHeraldo.Dtos;
using SfWebAPIHeraldo.Models;

namespace SfWebAPIHeraldo.Validators.Partners;

public class UpdatePartnerDtoValidator : AbstractValidator<UpdatePartnerDto>
{
    public UpdatePartnerDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name é obrigatório.")
            .Must(name =>
                !string.IsNullOrWhiteSpace(name)
                && name.Trim().Length >= 2
                && name.Trim().Length <= 50
            )
            .WithMessage("Name é obrigatório.")
            .Length(2, 50)
            .WithMessage("Por favor adicionar entre 2 e 100 caracteres.");

        RuleFor(x => x.TaxNumber)
            .NotEmpty()
            .Matches(@"^\d{9}$")
            .WithMessage("TaxNumber é obrigatório e deve ter exatamente 9 dígitos numéricos.");

        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type =>
            {
                return !int.TryParse(type, out _)
                    && Enum.TryParse<PartnerType>(type, true, out var parsed)
                    && Enum.IsDefined(parsed);
            })
            .WithMessage(
                $"Type é obrigatório. Os valores permitidos são: {string.Join(", ", Enum.GetNames(typeof(PartnerType)))}"
            );

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => x.Email != null)
            .WithMessage("Adicione um email válido.");

        RuleFor(x => x.CreditLimit)
            .NotNull()
            .InclusiveBetween(0m, 1000000m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: false)
            .WithMessage(
                "CreditLimit é decimal, entre 0 e 1.000.000, com o máximo de duas casas decimais."
            );

        RuleFor(x => x.IsActive)
            .NotNull()
            .WithMessage("O campo IsActive é obrigatório e deve ser True ou False.");

        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version é obrigatória para atualizar registos.")
            .Must(version => string.IsNullOrWhiteSpace(version) || IsValidRowVersion(version))
            .WithMessage("Version deve ser um token Base64 válido de 8 bytes.");
    }

    private static bool IsValidRowVersion(string version)
    {
        Span<byte> bytes = stackalloc byte[8];
        return Convert.TryFromBase64String(version, bytes, out var bytesWritten)
            && bytesWritten == bytes.Length;
    }
}
