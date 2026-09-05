using OFC.Modules.Sync;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class SyncRulesTests
{
    [Fact]
    public void An_empty_idempotency_key_is_invalid()
    {
        Assert.False(SyncRules.ValidIdempotencyKey(Guid.Empty));
        Assert.True(SyncRules.ValidIdempotencyKey(Guid.NewGuid()));
    }

    [Fact]
    public void Only_known_operation_types_are_accepted()
    {
        Assert.True(SyncRules.ValidOperationType("order.create"));
        Assert.True(SyncRules.ValidOperationType("inventory.movement.post"));
        Assert.False(SyncRules.ValidOperationType("order.delete"));
        Assert.False(SyncRules.ValidOperationType(""));
        Assert.False(SyncRules.ValidOperationType(null));
        Assert.False(SyncRules.ValidOperationType(new string('x', 41)));
    }

    [Fact]
    public void A_batch_requires_a_positive_and_bounded_operation_count()
    {
        Assert.True(SyncRules.ValidBatchSize(1));
        Assert.True(SyncRules.ValidBatchSize(200));
        Assert.False(SyncRules.ValidBatchSize(0));
        Assert.False(SyncRules.ValidBatchSize(201));
        Assert.False(SyncRules.ValidBatchSize(-1));
    }

    [Fact]
    public void Staleness_is_detected_only_when_the_published_catalog_is_newer()
    {
        Assert.True(SyncRules.IsStaleCatalog(4, 5));
        Assert.False(SyncRules.IsStaleCatalog(5, 5));
        Assert.False(SyncRules.IsStaleCatalog(6, 5));
        Assert.False(SyncRules.IsStaleCatalog(null, 5));
        Assert.False(SyncRules.IsStaleCatalog(1, 0));
    }

    [Fact]
    public void A_version_behind_the_server_is_stale_only_when_it_lags()
    {
        Assert.True(SyncRules.IsVersionBehind(10, 9));
        Assert.False(SyncRules.IsVersionBehind(10, 10));
        Assert.False(SyncRules.IsVersionBehind(10, 11));
        Assert.False(SyncRules.IsVersionBehind(10, null));
    }

    [Fact]
    public void An_operation_is_settled_once_applied_duplicated_or_conflicted()
    {
        Assert.True(SyncRules.IsSettled(SyncOperationStatus.Applied));
        Assert.True(SyncRules.IsSettled(SyncOperationStatus.Duplicate));
        Assert.True(SyncRules.IsSettled(SyncOperationStatus.Conflict));
        Assert.False(SyncRules.IsSettled(SyncOperationStatus.Received));
        Assert.False(SyncRules.IsSettled(SyncOperationStatus.Failed));
    }

    [Fact]
    public void Unsettled_operations_can_be_reapplied()
    {
        Assert.True(SyncRules.CanApply(SyncOperationStatus.Received));
        Assert.True(SyncRules.CanApply(SyncOperationStatus.Failed));
        Assert.False(SyncRules.CanApply(SyncOperationStatus.Applied));
        Assert.False(SyncRules.CanApply(SyncOperationStatus.Duplicate));
        Assert.False(SyncRules.CanApply(SyncOperationStatus.Conflict));
    }

    [Fact]
    public void Each_operation_type_maps_to_its_required_permission()
    {
        Assert.Equal("orders.manage", SyncRules.RequiredPermission("order.create"));
        Assert.Equal("inventory.movements.manage", SyncRules.RequiredPermission("inventory.movement.post"));
        Assert.Equal(string.Empty, SyncRules.RequiredPermission("uknown.type"));
        Assert.Equal(string.Empty, SyncRules.RequiredPermission(null));
    }

    [Fact]
    public void Unknown_operation_types_are_not_recognised()
    {
        Assert.True(SyncRules.IsKnownOperationType("order.create"));
        Assert.False(SyncRules.IsKnownOperationType("payment.record"));
        Assert.False(SyncRules.IsKnownOperationType(null));
    }
}
