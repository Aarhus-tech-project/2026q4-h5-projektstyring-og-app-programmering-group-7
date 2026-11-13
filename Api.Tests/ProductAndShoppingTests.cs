using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

[Collection(ApiCollection.Name)]
public class ProductAndShoppingTests(ApiFactory factory)
{
    [Fact]
    public async Task Product_lookup_uses_the_cache()
    {
        // Seed the cache so the test doesn't depend on Open Food Facts being online
        var barcode = "57" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
            db.ProductCache.Add(new ProductCache { Barcode = barcode, Found = true, Name = "Letmælk", Brand = "Arla" });
            db.SaveChanges();
        }
        var (client, _) = await factory.CreateUserClientAsync();

        var product = await client.GetFromJsonAsync<ProductResult>($"/api/products/{barcode}");

        Assert.Equal("Letmælk", product!.Name);
        Assert.Equal("Arla", product.Brand);
    }

    [Theory]
    [InlineData("123")] // too short
    [InlineData("12345678abc")] // not digits
    public async Task Invalid_barcode_returns_400(string barcode)
    {
        var (client, _) = await factory.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/products/{barcode}")).StatusCode);
    }

    [Fact]
    public void Open_Food_Facts_response_is_parsed()
    {
        var json = JsonDocument.Parse("""
            {"status":1,"product":{"product_name":" Letmælk ","brands":"Arla, Arla Foods","image_front_small_url":"https://img/x.jpg"}}
            """);

        var p = OpenFoodFactsService.Parse("5701234567890", json.RootElement);

        Assert.True(p.Found);
        Assert.Equal("Letmælk", p.Name);
        Assert.Equal("Arla", p.Brand);
        Assert.Equal("https://img/x.jpg", p.ImageUrl);
    }

    [Theory]
    [InlineData("""{"status":0,"status_verbose":"product not found"}""")]
    [InlineData("""{"status":1,"product":{"brands":"NoName"}}""")] // no product name
    public void Unknown_or_nameless_product_is_not_found(string json)
    {
        var p = OpenFoodFactsService.Parse("12345678", JsonDocument.Parse(json).RootElement);
        Assert.False(p.Found);
    }

    [Fact]
    public async Task Shopping_list_add_check_and_clear()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        var url = $"/api/households/{household.Id}/shopping";

        var eggs = await (await client.PostAsJsonAsync(url, new { name = "Eggs" })).Content.ReadFromJsonAsync<ShoppingResult>();
        await client.PostAsJsonAsync(url, new { name = "Bread" });
        await client.PatchAsJsonAsync($"{url}/{eggs!.Id}", new { @checked = true });

        var list = await client.GetFromJsonAsync<List<ShoppingResult>>(url);
        Assert.Equal(["Bread", "Eggs"], list!.Select(s => s.Name)); // unchecked first

        await client.DeleteAsync($"{url}/checked");
        Assert.Equal(["Bread"], (await client.GetFromJsonAsync<List<ShoppingResult>>(url))!.Select(s => s.Name));
    }
}
