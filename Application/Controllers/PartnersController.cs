using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SfWebAPIHeraldo.Dtos;
using SfWebAPIHeraldo.Services;

namespace SfWebAPIHeraldo.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PartnersController(IPartnerService partnerService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> InsertPartner(
        [FromBody] CreatePartnerDto dto,
        CancellationToken cancellationToken
    )
    {
        var partner = await partnerService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = partner.Id }, partner);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePartner(
        Guid id,
        [FromBody] UpdatePartnerDto dto,
        CancellationToken cancellationToken
    )
    {
        var result = await partnerService.UpdateAsync(id, dto, cancellationToken);

        if (result.NotFound)
            return NotFound(
                new
                {
                    Status = 404,
                    ErrorCode = "NOT_FOUND",
                    Message = "Registo não encontrado.",
                }
            );

        if (result.Conflict)
            return Conflict(
                new
                {
                    Status = 409,
                    ErrorCode = "CONCURRENCY_CONFLICT",
                    Message = "O registo foi modificado por outro utilizador. Recarregue e tente novamente.",
                }
            );

        return Ok(result.Partner);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] bool includeInactive = false,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var (items, totalCount, responsePage, responsePageSize) = await partnerService.GetAllAsync(
            search,
            type,
            includeInactive,
            page,
            pageSize,
            cancellationToken
        );

        return Ok(
            new
            {
                Items = items,
                TotalCount = totalCount,
                Page = responsePage,
                PageSize = responsePageSize,
            }
        );
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var partner = await partnerService.GetByIdAsync(id, cancellationToken);
        return partner is null
            ? NotFound(
                new
                {
                    Status = 404,
                    ErrorCode = "NOT_FOUND",
                    Message = "Registo não encontrado.",
                }
            )
            : Ok(partner);
    }
}
