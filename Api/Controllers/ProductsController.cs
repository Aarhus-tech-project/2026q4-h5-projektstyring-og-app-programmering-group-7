using System.Text.RegularExpressions;
using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Barcode lookup (Open Food Facts, cached).</summary>
[ApiController]
[Authorize]
[Route("api/products")]
public partial class ProductsController(OpenFoodFactsService products) : ControllerBase
{
    [GeneratedRegex(@"^\d{8,14}$")]
    private static partial Regex BarcodePattern();

    [HttpGet("{barcode}")]
    public async Task<ActionResult<ProductDto>> Lookup(string barcode, CancellationToken ct)
    {
        if (!BarcodePattern().IsMatch(barcode))
            return BadRequest(new { message = "Barcode must be 8–14 digits." });

        var product = await products.LookupAsync(barcode, ct);
        return product is null ? NotFound(new { message = "Product not found. Enter it manually." }) : Ok(product);
    }
}
