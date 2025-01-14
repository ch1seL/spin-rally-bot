namespace SpinRallyBot;

public class SqliteDbContext : AppDbContext {
    public SqliteDbContext(DbContextOptions<SqliteDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
