using System.Text.Json;
using Api.Data;
using Api.Dtos;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Looks up products by barcode in Open Food Facts (https://world.openfoodfacts.org).
/// Results (including "not found") are cached in the database, so a barcode is only
/// fetched from the internet once per <see cref="CacheLifetime"/>.
/// </summary>
public class OpenFoodFactsService(HttpClient http, AppDbContext db, ILogger<OpenFoodFactsService> logger)
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);

    public async Task<ProductDto?> LookupAsync(string barcode, CancellationToken ct)
    {
        var cached = await db.ProductCache.FindAsync([barcode], ct);
        if (cached is not null && cached.FetchedAt > DateTime.UtcNow - CacheLifetime)
            return cached.Found ? ToDto(cached) : null;

        ProductCache fresh;
        try
        {
            fresh = await FetchAsync(barcode, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Open Food Facts down/slow: fall back to an old cache entry if we have one
            logger.LogWarning(e, "Open Food Facts lookup failed for {Barcode}", barcode);
            return cached is { Found: true } ? ToDto(cached) : null;
        }

        if (cached is null) db.ProductCache.Add(fresh);
        else db.Entry(cached).CurrentValues.SetValues(fresh);
        await db.SaveChangesAsync(ct);

        return fresh.Found ? ToDto(fresh) : null;
    }

    private async Task<ProductCache> FetchAsync(string barcode, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            $"api/v2/product/{barcode}.json?fields=product_name,brands,image_front_small_url,image_url", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new ProductCache { Barcode = barcode, Found = false };
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return Parse(barcode, doc.RootElement);
    }

    /// <summary>Turns an Open Food Facts v2 product response into a cache entry.</summary>
    public static ProductCache Parse(string barcode, JsonElement root)
    {
        var found = root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Number && status.GetInt32() == 1;
        if (!found || !root.TryGetProperty("product", out var product))
            return new ProductCache { Barcode = barcode, Found = false };

        string? Get(string name) =>
            product.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString()!.Trim()
                : null;

        var name = Get("product_name");
        return new ProductCache
        {
            Barcode = barcode,
            Found = name is not null,
            Name = Truncate(name, 200),
            // "brands" is a comma-separated list; the first is the main brand
            Brand = Truncate(Get("brands")?.Split(',')[0].Trim(), 200),
            ImageUrl = Truncate(Get("image_front_small_url") ?? Get("image_url"), 500),
        };
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];

    private static ProductDto ToDto(ProductCache p) => new(p.Barcode, p.Name!, p.Brand, p.ImageUrl);
}
