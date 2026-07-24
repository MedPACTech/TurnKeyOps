using IBeam.Identity.Events;
using IBeam.Identity.Interfaces;
using MedInsights.Services.Interfaces;


namespace MedInsights.Services.Events;

public sealed class UserProfileHook : IAuthLifecycleHook
{
    private readonly IUserProfileService _profiles;

    public UserProfileHook(IUserProfileService profiles)
    {
        _profiles = profiles;
    }

    public Task OnAuthUserCreatedAsync(AuthUserCreatedEvent evt, CancellationToken ct = default) => Task.CompletedTask;

    public Task OnTenantCreatedAsync(TenantCreatedEvent evt, CancellationToken ct = default) => Task.CompletedTask;

    public async Task OnTenantUserLinkedAsync(TenantUserLinkedEvent evt, CancellationToken ct = default)
    {
        if (!Guid.TryParse(evt.AuthUserId, out var userId)) return;
        await _profiles.EnsureProfileExistsAsync(evt.TenantId, userId, ct);
        await _profiles.CreateUserProfileAsync(evt.TenantId, userId, ct);
    }

    public async Task OnLoginSucceededAsync(LoginSucceededEvent evt, CancellationToken ct = default)
    {
        if (!Guid.TryParse(evt.AuthUserId, out var userId)) return;
        Console.WriteLine($"Login succeeded for user {userId} in tenant {evt.TenantId}");
        return;
        //await _profiles.EnsureProfileExistsAsync(evt.AuthUserId, userId, ct);
    }
}
