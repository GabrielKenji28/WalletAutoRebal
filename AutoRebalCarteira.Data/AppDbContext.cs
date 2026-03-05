using AutoRebalCarteira.Domain.Entities;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace AutoRebalCarteira.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ContaGrafica> ContasGraficas => Set<ContaGrafica>();
    public DbSet<CustodiaItem> CustodiaItens => Set<CustodiaItem>();
    public DbSet<CestaRecomendacao> CestasRecomendacao => Set<CestaRecomendacao>();
    public DbSet<CestaItem> CestaItens => Set<CestaItem>();
    public DbSet<OrdemCompra> OrdensCompra => Set<OrdemCompra>();
    public DbSet<OrdemCompraItem> OrdensCompraItens => Set<OrdemCompraItem>();
    public DbSet<Distribuicao> Distribuicoes => Set<Distribuicao>();
    public DbSet<DistribuicaoItem> DistribuicaoItens => Set<DistribuicaoItem>();
    public DbSet<HistoricoValorMensal> HistoricoValoresMensais => Set<HistoricoValorMensal>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var agora = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntidadeBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CriadoEm = agora;
                    if (!entry.Entity.Ativo)
                        entry.Entity.Ativo = true;
                    break;
                case EntityState.Modified:
                    entry.Entity.AtualizadoEm = agora;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Cpf).IsUnique();
            entity.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ValorMensal).HasPrecision(18, 2);

            entity.HasOne(e => e.ContaGrafica)
                .WithOne(c => c.Cliente)
                .HasForeignKey<ContaGrafica>(c => c.ClienteId);
        });

        modelBuilder.Entity<ContaGrafica>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroConta).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Tipo).HasMaxLength(10).IsRequired();

            entity.HasMany(e => e.Custodia)
                .WithOne(c => c.ContaGrafica)
                .HasForeignKey(c => c.ContaGraficaId);
        });

        modelBuilder.Entity<CustodiaItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoMedio).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.ContaGraficaId, e.Ticker });
        });

        modelBuilder.Entity<CestaRecomendacao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nome).HasMaxLength(100).IsRequired();

            entity.HasMany(e => e.Itens)
                .WithOne(i => i.CestaRecomendacao)
                .HasForeignKey(i => i.CestaRecomendacaoId);
        });

        modelBuilder.Entity<CestaItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.Percentual).HasPrecision(5, 2);
        });

        modelBuilder.Entity<OrdemCompra>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValorTotalConsolidado).HasPrecision(18, 2);

            entity.HasMany(e => e.Itens)
                .WithOne(i => i.OrdemCompra)
                .HasForeignKey(i => i.OrdemCompraId);

            entity.HasMany(e => e.Distribuicoes)
                .WithOne(d => d.OrdemCompra)
                .HasForeignKey(d => d.OrdemCompraId);
        });

        modelBuilder.Entity<OrdemCompraItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoUnitario).HasPrecision(18, 2);
            entity.Property(e => e.ValorTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Distribuicao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValorAporte).HasPrecision(18, 2);

            entity.HasMany(e => e.Itens)
                .WithOne(i => i.Distribuicao)
                .HasForeignKey(i => i.DistribuicaoId);
        });

        modelBuilder.Entity<DistribuicaoItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoUnitario).HasPrecision(18, 2);
        });

        modelBuilder.Entity<HistoricoValorMensal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValorAnterior).HasPrecision(18, 2);
            entity.Property(e => e.ValorNovo).HasPrecision(18, 2);
        });

        // Filtros globais e indexes de Ativo para todas as entidades
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(EntidadeBase).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var ativoProperty = Expression.Property(parameter, nameof(EntidadeBase.Ativo));
            var filter = Expression.Lambda(ativoProperty, parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);

            modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(EntidadeBase.Ativo));
        }

        // Seed: Conta Master
        modelBuilder.Entity<ContaGrafica>().HasData(new ContaGrafica
        {
            Id = 1,
            NumeroConta = "MST-000001",
            Tipo = "MASTER",
            DataCriacao = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CriadoEm = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Ativo = true,
            ClienteId = null
        });
    }
}
