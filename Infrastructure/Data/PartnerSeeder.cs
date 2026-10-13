using Microsoft.EntityFrameworkCore;
using SfWebAPIHeraldo.Models;

namespace SfWebAPIHeraldo.Data;

public static class PartnerSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Partners.AnyAsync())
            return;

        var partners = new[]
        {
            new Partner(
                "Ana Silva",
                "001234567",
                PartnerType.CUSTOMER,
                "ana.silva@email.com",
                5000m
            ),
            new Partner(
                "João Costa",
                "987654321",
                PartnerType.SUPPLIER,
                "joao.costa@email.com",
                3000m
            ),
            new Partner(
                "Maria Santos",
                "123456789",
                PartnerType.BOTH,
                "maria.santos@email.com",
                7500m
            ),
            new Partner(
                "Pedro Oliveira",
                "112233445",
                PartnerType.CUSTOMER,
                "pedro.oliveira@email.com",
                2500m
            ),
            new Partner(
                "Lia Martins",
                "556677889",
                PartnerType.SUPPLIER,
                "lia.martins@email.com",
                1500m
            ),
        };

        await context.Partners.AddRangeAsync(partners);
        await context.SaveChangesAsync();
    }
}
