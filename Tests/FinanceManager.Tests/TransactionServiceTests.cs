using FinanceManager2._0.Models;
using FinanceManager2._0.Services;
using FinanceManager2._0.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests;

public class TransactionServiceTests
{
    [Fact]
    public async Task BuildQuery_FiltersByAmountRangeDateRangeAndCategory()
    {
        await using var context = TestHelpers.CreateContext();
        await TestHelpers.SeedCategoriesAsync(context);
        context.Transactions.AddRange(
            new Transaction { Id = 1, UserId = "user1", CategoryId = 1, Amount = 10, Date = new DateTime(2024, 1, 1) },
            new Transaction { Id = 2, UserId = "user1", CategoryId = 1, Amount = 50, Date = new DateTime(2024, 1, 15) },
            new Transaction { Id = 3, UserId = "user1", CategoryId = 3, Amount = 60, Date = new DateTime(2024, 1, 20) },
            new Transaction { Id = 4, UserId = "user1", CategoryId = 1, Amount = 90, Date = new DateTime(2024, 2, 1) },
            new Transaction { Id = 5, UserId = "user2", CategoryId = 1, Amount = 50, Date = new DateTime(2024, 1, 15) }
        );
        await context.SaveChangesAsync();

        var filter = new TransactionFilterViewModel
        {
            MinAmount = 20,
            MaxAmount = 70,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 1, 31),
            CategoryId = 1
        };

        var service = new TransactionService(context);
        var result = await service.BuildQuery("user1", false, filter).ToListAsync();

        var item = Assert.Single(result);
        Assert.Equal(2, item.Id);
        Assert.Equal(50, item.Amount);
    }

    [Fact]
    public async Task BuildQuery_CategoryZeroMeansAllCategories()
    {
        await using var context = TestHelpers.CreateContext();
        await TestHelpers.SeedCategoriesAsync(context);
        context.Transactions.AddRange(
            new Transaction { Id = 1, UserId = "user1", CategoryId = 1, Amount = 10, Date = DateTime.UtcNow },
            new Transaction { Id = 2, UserId = "user1", CategoryId = 3, Amount = 20, Date = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new TransactionService(context);
        var result = await service.BuildQuery("user1", false, new TransactionFilterViewModel { CategoryId = 0 }).ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task BuildQuery_ForAdminDoesNotLimitByUser()
    {
        await using var context = TestHelpers.CreateContext();
        await TestHelpers.SeedCategoriesAsync(context);
        context.Transactions.AddRange(
            new Transaction { UserId = "user1", CategoryId = 1, Amount = 10, Date = DateTime.UtcNow },
            new Transaction { UserId = "user2", CategoryId = 1, Amount = 20, Date = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new TransactionService(context);
        var result = await service.BuildQuery("admin", true, new TransactionFilterViewModel()).ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CreateUpdateDelete_ProtectsOwnership()
    {
        await using var context = TestHelpers.CreateContext();
        var service = new TransactionService(context);
        await service.CreateAsync(new Transaction { Id = 1, CategoryId = 1, Amount = 25, Date = new DateTime(2024, 1, 1), Note = "created" }, "user1");

        Assert.False(await service.UpdateAsync(1, new Transaction { Id = 1, CategoryId = 1, Amount = 99, Date = DateTime.UtcNow }, "user2", false));
        Assert.False(await service.DeleteAsync(1, "user2", false));
        Assert.True(await service.UpdateAsync(1, new Transaction { Id = 1, CategoryId = 3, Amount = 30, Date = DateTime.UtcNow, Note = "updated" }, "user1", false));
        Assert.True(await service.DeleteAsync(1, "admin", true));
        Assert.Empty(context.Transactions);
    }
}
