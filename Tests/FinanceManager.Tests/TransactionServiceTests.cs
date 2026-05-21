using FinanceManager2._0.Models;
using FinanceManager2._0.Services;
using FinanceManager2._0.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests;

public class TransactionServiceTests
{
    [Fact]
    public async Task BuildQuery_SearchesByNote_CaseInsensitive_AndOnlyCurrentUser()
    {
        await using var context = TestHelpers.CreateContext();
        await TestHelpers.SeedCategoriesAsync(context);
        context.Transactions.AddRange(
            new Transaction { Id = 1, UserId = "user1", CategoryId = 1, Amount = 20, Note = "Coffee with friends", Date = DateTime.UtcNow },
            new Transaction { Id = 2, UserId = "user1", CategoryId = 1, Amount = 50, Note = "rent", Date = DateTime.UtcNow },
            new Transaction { Id = 3, UserId = "user2", CategoryId = 1, Amount = 10, Note = "coffee", Date = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new TransactionService(context);
        var result = await service.BuildQuery("user1", false, new TransactionFilterViewModel { SearchKeyword = "coFfEe" }).ToListAsync();

        var item = Assert.Single(result);
        Assert.Equal(1, item.Id);
        Assert.Equal("user1", item.UserId);
    }

    [Fact]
    public async Task BuildQuery_SearchesByCategoryName()
    {
        await using var context = TestHelpers.CreateContext();
        await TestHelpers.SeedCategoriesAsync(context);
        context.Transactions.AddRange(
            new Transaction { Id = 1, UserId = "user1", CategoryId = 1, Amount = 20, Note = null, Date = DateTime.UtcNow },
            new Transaction { Id = 2, UserId = "user1", CategoryId = 3, Amount = 50, Note = "bus", Date = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new TransactionService(context);
        var result = await service.BuildQuery("user1", false, new TransactionFilterViewModel { SearchKeyword = "food" }).ToListAsync();

        var item = Assert.Single(result);
        Assert.Equal(1, item.CategoryId);
    }

    [Fact]
    public async Task BuildQuery_EmptyKeywordReturnsAllOwnTransactions()
    {
        await using var context = TestHelpers.CreateContext();
        await TestHelpers.SeedCategoriesAsync(context);
        context.Transactions.AddRange(
            new Transaction { UserId = "user1", CategoryId = 1, Amount = 20, Date = DateTime.UtcNow },
            new Transaction { UserId = "user1", CategoryId = 3, Amount = 50, Date = DateTime.UtcNow },
            new Transaction { UserId = "user2", CategoryId = 3, Amount = 50, Date = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new TransactionService(context);
        var result = await service.BuildQuery("user1", false, new TransactionFilterViewModel { SearchKeyword = "   " }).ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal("user1", t.UserId));
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
