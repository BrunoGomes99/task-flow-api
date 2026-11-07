namespace TaskFlow.Infrastructure.Tests.Health;

public sealed class LivenessRouteTests
{
    [Fact]
    public void Hosts_ExposeLiveAndNotTheLegacyHealthRoute()
    {
        var root = RepoRoot();

        var controller = File.ReadAllText(Path.Combine(root, "src", "TaskFlow.Api", "Controllers", "HealthController.cs"));
        Assert.Contains("[Route(\"/health/live\")]", controller);
        Assert.DoesNotContain("[Route(\"/health\")]", controller);

        foreach (var relative in new[] { Path.Combine("src", "TaskFlow.Scheduler", "Program.cs"), Path.Combine("src", "TaskFlow.Notifications", "Program.cs") })
        {
            var program = File.ReadAllText(Path.Combine(root, relative));
            Assert.Contains("MapGet(\"/health/live\"", program);
            Assert.DoesNotContain("MapGet(\"/health\"", program);
        }

        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        Assert.Contains("http://127.0.0.1:8080/health/live", compose);
        Assert.DoesNotContain("http://127.0.0.1:8080/health\"", compose);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TaskFlow.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }
}
