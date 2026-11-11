using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MineOS.Tests.Integration;

public sealed class ImportUploadEndpointTests(MineOsWebApplicationFactory factory) : IClassFixture<MineOsWebApplicationFactory>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    public async Task Upload_RequiresAnAdministrator(string? role, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        if (role is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/host/imports/upload")
            { Content = new ByteArrayContent([80, 75, 3, 4]) };
        request.Headers.Add("X-File-Name", "blocked.zip");
        Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
        Assert.False(File.Exists(Path.Combine(factory.HostRoot, "import", "blocked.zip")));
    }

    [Fact]
    public async Task Upload_StreamsTheCompleteArchiveToTheImportDirectory()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("admin"));
        byte[] bytes = [80, 75, 3, 4, 1, 2, 3, 4, 5];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/host/imports/upload")
            { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        request.Headers.Add("X-File-Name", "complete.zip");
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
        var path = Path.Combine(factory.HostRoot, "import", "complete.zip");
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".upload-*.tmp"));
    }

    private static string Token(string role) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "MineOS.Test", "MineOS.Test", [new(ClaimTypes.NameIdentifier, "1"), new(ClaimTypes.Role, role)],
        expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-signing-key-at-least-32-characters-long!!")), SecurityAlgorithms.HmacSha256)));
}
