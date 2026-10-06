using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EduAnalytics.DataAccess.Context;

/// <summary>
/// EF Core CLI komutları (migrations add / database update) için tasarım-zamanı context factory.
/// WPF projesi referans verilmeden migration komutlarının çalışmasını sağlar.
/// Connection string'i farklı bir MSSQL instance kullanıyorsan aşağıdan düzenle.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EduAnalyticsDbContext>
{
    public EduAnalyticsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=EduAnalyticsDb;" +
            "Trusted_Connection=true;" +
            "TrustServerCertificate=true;";

        var options = new DbContextOptionsBuilder<EduAnalyticsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new EduAnalyticsDbContext(options);
    }
}
