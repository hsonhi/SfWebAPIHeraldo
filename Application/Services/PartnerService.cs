using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SfWebAPIHeraldo.Data;
using SfWebAPIHeraldo.Dtos;
using SfWebAPIHeraldo.Models;

namespace SfWebAPIHeraldo.Services;

public interface IPartnerService
{
    Task<Partner> CreateAsync(CreatePartnerDto dto, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Partner> Items, int TotalCount, int Page, int PageSize)> GetAllAsync(
        string? search,
        string? type,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );
    Task<Partner?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PartnerUpdateResult> UpdateAsync(
        Guid id,
        UpdatePartnerDto dto,
        CancellationToken cancellationToken
    );
}

public sealed record PartnerUpdateResult(Partner? Partner, bool NotFound, bool Conflict);

public sealed class PartnerService(AppDbContext context) : IPartnerService
{
    public async Task<Partner> CreateAsync(
        CreatePartnerDto dto,
        CancellationToken cancellationToken
    )
    {
        var partner = new Partner(
            dto.Name,
            dto.TaxNumber,
            Enum.Parse<PartnerType>(dto.Type, ignoreCase: true),
            dto.Email,
            dto.CreditLimit
        );

        context.Partners.Add(partner);
        await context.SaveChangesAsync(cancellationToken);
        return partner;
    }

    public async Task<(
        IReadOnlyList<Partner> Items,
        int TotalCount,
        int Page,
        int PageSize
    )> GetAllAsync(
        string? search,
        string? type,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var query = context.Partners.AsNoTracking();

        if (!includeInactive)
            query = query.Where(partner => partner.IsActive);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (
                !Enum.TryParse<PartnerType>(type, ignoreCase: true, out var requestedType)
                || !Enum.IsDefined(requestedType)
            )
            {
                throw new ArgumentException("Type inválido.");
            }

            query = requestedType switch
            {
                PartnerType.CUSTOMER => query.Where(partner =>
                    partner.Type == PartnerType.CUSTOMER || partner.Type == PartnerType.BOTH
                ),
                PartnerType.SUPPLIER => query.Where(partner =>
                    partner.Type == PartnerType.SUPPLIER || partner.Type == PartnerType.BOTH
                ),
                PartnerType.BOTH => query.Where(partner => partner.Type == PartnerType.BOTH),
                _ => query,
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var nameSearch = search.Trim().ToLowerInvariant();
            var taxNumberSearch = TaxNumberNormalizer.Normalize(search);

            query = string.IsNullOrEmpty(taxNumberSearch)
                ? query.Where(partner => partner.Name.ToLower().Contains(nameSearch))
                : query.Where(partner =>
                    partner.Name.ToLower().Contains(nameSearch)
                    || partner.TaxNumber.Contains(taxNumberSearch)
                );
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        var items =
            offset >= totalCount
                ? new List<Partner>()
                : await query
                    .OrderBy(partner => partner.Name)
                    .ThenBy(partner => partner.Id)
                    .Skip((int)offset)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

        return (items, totalCount, page, pageSize);
    }

    public async Task<Partner?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Partners.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken
        );
    }

    public async Task<PartnerUpdateResult> UpdateAsync(
        Guid id,
        UpdatePartnerDto dto,
        CancellationToken cancellationToken
    )
    {
        var partner = await context.Partners.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken
        );

        if (partner is null)
            return new PartnerUpdateResult(null, true, false);

        var submittedVersion = Convert.FromBase64String(dto.Version);
        if (!submittedVersion.SequenceEqual(partner.Version))
            return new PartnerUpdateResult(null, false, true);

        partner.Update(
            dto.Name,
            dto.TaxNumber,
            Enum.Parse<PartnerType>(dto.Type, ignoreCase: true),
            dto.Email,
            dto.CreditLimit,
            dto.IsActive ?? false
        );

        await context.SaveChangesAsync(cancellationToken);
        return new PartnerUpdateResult(partner, false, false);
    }
}
