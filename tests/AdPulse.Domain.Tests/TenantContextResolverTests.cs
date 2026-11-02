using System.Security.Claims;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Domain.Tests;

public sealed class TenantContextResolverTests
{
    [Fact]
    public async Task Resolves_tenant_only_after_server_membership_check()
    {
        var store = new StubMembershipStore(new TenantMembership("tenant-a", "user-1", new HashSet<string> { "analyst" }));
        var principal = Principal("user-1", "tenant-a");

        var context = await new TenantContextResolver(store).ResolveAsync(principal);

        Assert.Equal("tenant-a", context.TenantId);
        Assert.True(context.IsInRole("analyst"));
        Assert.Equal(("user-1", "tenant-a"), store.LastLookup!.Value);
    }

    [Fact]
    public async Task Rejects_tenant_without_membership()
    {
        var resolver = new TenantContextResolver(new StubMembershipStore(null));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => resolver.ResolveAsync(Principal("user-1", "tenant-b")));
    }

    [Fact]
    public async Task Rejects_duplicate_tenant_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("tenant_id", "tenant-b")
        ], "test"));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => new TenantContextResolver(new StubMembershipStore(null)).ResolveAsync(principal));
    }

    [Fact]
    public async Task Rejects_duplicate_claims_with_the_same_value()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("tenant_id", "tenant-a")
        ], "test"));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => new TenantContextResolver(new StubMembershipStore(null)).ResolveAsync(principal));
    }

    [Fact]
    public async Task Rejects_membership_that_does_not_match_the_validated_claims()
    {
        var store = new StubMembershipStore(new TenantMembership("tenant-b", "user-2", new HashSet<string>()));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => new TenantContextResolver(store).ResolveAsync(Principal("user-1", "tenant-a")));
    }

    [Fact]
    public async Task Passes_cancellation_to_membership_lookup()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new StubMembershipStore(
            new TenantMembership("tenant-a", "user-1", new HashSet<string>()),
            honorCancellation: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TenantContextResolver(store).ResolveAsync(Principal("user-1", "tenant-a"), cancellation.Token));

        Assert.Equal(cancellation.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task Does_not_use_a_client_tenant_fallback_when_tenant_claim_is_missing()
    {
        var store = new StubMembershipStore(new TenantMembership("tenant-a", "user-1", new HashSet<string>()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("TenantId", "tenant-a")
        ], "test"));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => new TenantContextResolver(store).ResolveAsync(principal));

        Assert.Null(store.LastLookup);
    }

    [Fact]
    public async Task Rejects_a_tenant_claim_that_exceeds_the_tenant_scope_limit()
    {
        var store = new StubMembershipStore(null);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            new TenantContextResolver(store).ResolveAsync(Principal("user-1", new string('t', 65))));

        Assert.Null(store.LastLookup);
    }

    private static ClaimsPrincipal Principal(string subject, string tenant) => new(new ClaimsIdentity(
        [new Claim("sub", subject), new Claim("tenant_id", tenant)], "test"));

    private sealed class StubMembershipStore(TenantMembership? membership, bool honorCancellation = false) : ITenantMembershipStore
    {
        public (string Subject, string Tenant)? LastLookup { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<TenantMembership?> FindAsync(string subjectId, string tenantId, CancellationToken cancellationToken)
        {
            LastLookup = (subjectId, tenantId);
            LastCancellationToken = cancellationToken;
            if (honorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Task.FromResult(membership);
        }
    }
}
