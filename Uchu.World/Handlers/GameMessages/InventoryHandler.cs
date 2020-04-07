using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Handlers.GameMessages
{
    public class InventoryHandler : HandlerGroup
    {
        [PacketHandler]
        public async Task ItemMovementHandler(MoveItemInInventoryMessage message, Player player)
        {
            if (message.DestinationInventoryType == InventoryType.Invalid)
                message.DestinationInventoryType = message.CurrentInventoryType;

            if (message.Item.Inventory.ManagerComponent.GameObject != player) return;

            await message.Item.SetSlotAsync((uint) message.NewSlot);
        }
        
        [PacketHandler]
        public async Task ItemMoveBetweenInventoriesHandler(MoveItemBetweenInventoryTypesMessage message, Player player)
        {
            await player.GetComponent<InventoryManagerComponent>().MoveItemsBetweenInventoriesAsync(
                message.Item,
                message.Lot,
                message.StackCount,
                message.SourceInventory,
                message.DestinationInventory
            );
        }

        [PacketHandler]
        public async Task RemoveItemHandler(RemoveItemToInventoryMessage message, Player player)
        {
            if (!message.Confirmed) return;

            if (message.Item == default) return;
            
            var inventoryManager = player.GetComponent<InventoryManagerComponent>();

            var count = await message.Item.GetCountAsync();
            
            await inventoryManager.RemoveItemAsync(message.Item.Lot, count - message.TotalItems, message.InventoryType, true);
        }

        [PacketHandler]
        public async Task EquipItemHandler(EquipItemMessage message, Player player)
        {
            if (message.Item == null) return;

            await message.Item.EquipAsync();
        }

        [PacketHandler]
        public async Task UnEquipItemHandler(UnEquipItemMessage message, Player player)
        {
            if (message.ItemToUnEquip == default)
            {
                Logger.Error($"{player} attempted to un equip invalid item.");
                
                return;
            }
            
            await message.ItemToUnEquip.UnEquipAsync();

            if (message.ReplacementItem != null)
                await message.ReplacementItem.EquipAsync();
        }
    }
}