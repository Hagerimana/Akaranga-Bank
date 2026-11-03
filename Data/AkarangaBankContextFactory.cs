using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AkarangaBankMvc.Web.Data;

public class AkarangaBankContextFactory
    : IDesignTimeDbContextFactory<AkarangaBankContext>
{
    public AkarangaBankContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<AkarangaBankContext>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("AkarangaBank");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "La chaîne ConnectionStrings:AkarangaBank est absente.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<AkarangaBankContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new AkarangaBankContext(optionsBuilder.Options);
    }
}