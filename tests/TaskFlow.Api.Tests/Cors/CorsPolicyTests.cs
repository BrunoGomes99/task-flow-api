using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using TaskFlow.Api.Extensions;

namespace TaskFlow.Api.Tests.Cors;

public sealed class CorsPolicyTests
{
    [Fact]
    public async Task Policy_AllowsLocalAngularOrigin_AndExplicitMethodsAndHeaders()
    {
        var policy = await BuildPolicyAsync(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
        });

        Assert.Equal("TaskFlow", CorsExtensions.PolicyName);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.SupportsCredentials);
        Assert.Equal("http://localhost:4200", Assert.Single(policy.Origins));
        Assert.Contains(HttpMethods.Get, policy.Methods);
        Assert.Contains(HttpMethods.Post, policy.Methods);
        Assert.Contains(HttpMethods.Put, policy.Methods);
        Assert.Contains(HttpMethods.Patch, policy.Methods);
        Assert.Contains(HttpMethods.Delete, policy.Methods);
        Assert.Contains(HeaderNames.Authorization, policy.Headers);
        Assert.Contains(HeaderNames.ContentType, policy.Headers);
        Assert.Contains(HeaderNames.Accept, policy.Headers);
    }

    [Fact]
    public async Task Policy_TrimsTrailingSlash_AndDropsBlankOrigins()
    {
        var policy = await BuildPolicyAsync(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = " http://localhost:4200/ ",
            ["Cors:AllowedOrigins:1"] = " ",
            ["Cors:AllowedOrigins:2"] = "http://localhost:4200"
        });

        Assert.Equal("http://localhost:4200", Assert.Single(policy.Origins));
    }

    [Fact]
    public async Task Policy_WithNoOrigins_AllowsNone_AndDoesNotThrow()
    {
        var policy = await BuildPolicyAsync(new Dictionary<string, string?>());

        Assert.Empty(policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.SupportsCredentials);
    }

    private static async Task<CorsPolicy> BuildPolicyAsync(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddTaskFlowCors(configuration);
        await using var provider = services.BuildServiceProvider();
        var policy = await provider.GetRequiredService<ICorsPolicyProvider>()
            .GetPolicyAsync(new DefaultHttpContext(), CorsExtensions.PolicyName);

        Assert.NotNull(policy);
        return policy;
    }
}
