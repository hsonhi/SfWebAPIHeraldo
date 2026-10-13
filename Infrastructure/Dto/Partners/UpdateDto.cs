using SfWebAPIHeraldo.Models;

namespace SfWebAPIHeraldo.Dtos
{
    public class UpdatePartnerDto
    {
        private string _name = string.Empty;
        private string? _email;
        private string _taxnumber = string.Empty;

        public string Name
        {
            get => _name;
            set => _name = value != null ? value.Trim() : string.Empty;
        }
        public string TaxNumber
        {
            get => _taxnumber;
            set => _taxnumber = value != null ? TaxNumberNormalizer.Normalize(value) : string.Empty;
        }
        public string Type { get; set; } = string.Empty;
        public string? Email
        {
            get => _email;
            set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        public decimal CreditLimit { get; set; }

        public bool? IsActive { get; set; }

        public string Version { get; set; } = string.Empty;
    }
}
