using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Entities;
using MineSafety.Api.Services;

namespace MineSafety.Api.Tests;

// A fresh, isolated "world" per test: an in-memory database with the normal development
// test data (DbSeeder) plus the real ReportService and AttachmentService.
public sealed class TestWorld : IDisposable
{
    public MineSafetyDbContext Db { get; }
    public ReportService Reports { get; }
    public AttachmentService Attachments { get; }
    private readonly string _uploadFolder;

    public TestWorld()
    {
        var options = new DbContextOptionsBuilder<MineSafetyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The in-memory database has no real transactions; that's fine for these tests.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new MineSafetyDbContext(options);
        DbSeeder.Seed(Db);

        // A second supervisor at Tunnel 7 / Stope 12B, so S2001's reports have a possible reviewer.
        var supervisorRole = Db.Roles.Single(r => r.RoleName == RoleNames.Supervisor).RoleId;
        var tunnel7 = Db.Locations.Single(l => l.LocationCode == "T-7").LocationId;
        var s2003 = new Account { WorkerId = "S2003", FullName = "Second Supervisor", Email = "s3@mine.test",
                                  PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow };
        s2003.AccountRoles.Add(new AccountRole { RoleId = supervisorRole, AssignedAt = DateTime.UtcNow, IsActive = true });
        s2003.AccountLocations.Add(new AccountLocation { LocationId = tunnel7, IsDefault = true, AssignedAt = DateTime.UtcNow, IsActive = true });
        Db.Accounts.Add(s2003);
        Db.SaveChanges();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:AttachmentsPath"] = "uploads",
            ["Web:ReportUrlTemplate"] = "http://localhost:5173/incidents/{id}"
        }).Build();

        _uploadFolder = Path.Combine(Path.GetTempPath(), "minesafe-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_uploadFolder);

        Reports = new ReportService(Db, config);
        Attachments = new AttachmentService(Db, Reports, config, new FakeEnvironment(_uploadFolder));
    }

    public int LocationId(string code) => Db.Locations.Single(l => l.LocationCode == code).LocationId;

    // The same CurrentUser the API builds from a token: current roles + allowed locations.
    public CurrentUser User(string workerId)
    {
        var account = Db.Accounts.Single(a => a.WorkerId == workerId);
        var roles = Db.AccountRoles.Where(ar => ar.AccountId == account.AccountId && ar.IsActive)
                                   .Select(ar => ar.Role.RoleName).ToList();
        var seesAll = roles.Any(r => RoleNames.SeeAllLocations.Contains(r));
        return new CurrentUser
        {
            AccountId = account.AccountId,
            Roles = roles,
            AllowedLocationIds = seesAll ? null : Db.AccountLocations
                .Where(al => al.AccountId == account.AccountId && al.IsActive)
                .Select(al => al.LocationId).ToList()
        };
    }

    public static IFormFile File(string mimeType, byte[] bytes, string fileName = "evidence")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = mimeType
        };
    }

    public void Dispose()
    {
        Db.Dispose();
        try { Directory.Delete(_uploadFolder, recursive: true); } catch { /* temp folder */ }
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string root) { ContentRootPath = root; WebRootPath = root; }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "MineSafetyApi.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
    }
}
