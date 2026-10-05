using System.ComponentModel.DataAnnotations;

namespace JensenOnline.Api.Dtos;

public class ProductQuery
{
    // Maxlängd på söktermen skyddar mot tunga sökningar (T8)
    [StringLength(100)]
    public string? Search { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    // Paginering: klienten kan aldrig hämta hela tabellen i ett anrop (T8)
    [Range(1, 50)]
    public int PageSize { get; set; } = 20;
}

public class ProductInputDto
{
    // Allowlist: bara bokstäver, siffror och vanliga tecken.
    // < och > avvisas, vilket stoppar HTML och skript i produktnamn (T4)
        [Required(ErrorMessage = "Namn måste anges.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Namnet måste vara 2–100 tecken.")]
        [RegularExpression(@"^[\p{L}\p{N} \-.,:()'&/+%""]+$", ErrorMessage = "Namnet innehåller otillåtna tecken.")]
    public string Name { get; set; } = "";

    // Fritext är tillåten, men frontend visar den alltid som text (textContent)
      [StringLength(1000, ErrorMessage = "Beskrivningen får vara högst 1 000 tecken.")]
    public string Description { get; set; } = "";

    // Invariant kultur, annars tolkas "0.01" fel på en svensk dator (decimalkomma)
    [Range(typeof(decimal), "0.01", "1000000",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Priset måste vara mellan 0,01 och 1 000 000.")]
    public decimal Price { get; set; }

       [Range(0, 100000, ErrorMessage = "Lagret måste vara mellan 0 och 100 000.")]
    public int Stock { get; set; }
}

public record ProductDto(int Id, string Name, string Description, decimal Price, int Stock);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);