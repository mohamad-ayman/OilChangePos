using System.Net.Http.Json;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpAuthService(HttpClient http) : IAuthService
{
    private sealed class LoginResponseDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? HomeBranchWarehouseId { get; set; }
    }

    public async Task<AppUser?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var res = await http.PostAsJsonAsync("api/Auth/login", new { username, password }, OilChangeJson.Options, cancellationToken);
        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
        var dto = await res.Content.ReadFromJsonAsync<LoginResponseDto>(OilChangeJson.Options, cancellationToken)
                  ?? throw new InvalidOperationException("Login response was empty.");
        return new AppUser
        {
            Id = dto.Id,
            Username = dto.Username,
            PasswordHash = string.Empty,
            Role = Enum.Parse<UserRole>(dto.Role, true),
            IsActive = dto.IsActive,
            HomeBranchWarehouseId = dto.HomeBranchWarehouseId
        };
    }

    public async Task<IReadOnlyList<BranchRoleUserDto>> ListBranchRoleUsersAsync(int adminUserId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Auth/branch-users?adminUserId={adminUserId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<BranchRoleUserDto>>(res, cancellationToken);
    }

    public async Task SetUserHomeBranchWarehouseAsync(int adminUserId, int targetUserId, int? homeBranchWarehouseId, CancellationToken cancellationToken = default)
    {
        var body = new { adminUserId, targetUserId, homeBranchWarehouseId };
        using var res = await http.PostAsJsonAsync("api/Auth/user-home-branch", body, OilChangeJson.Options, cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }
}
