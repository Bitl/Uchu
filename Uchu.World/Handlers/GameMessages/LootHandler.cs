using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Handlers.GameMessages
{
    public class LootHandler : HandlerGroup
    {
        [PacketHandler]
        public async Task PickupCurrencyHandler(PickupCurrencyMessage message, Player player)
        {
            if (message.Currency > player.EntitledCurrency)
            {
                Logger.Error($"{player} is trying to pick up more currency than they are entitled to.");
                return;
            }

            player.EntitledCurrency -= message.Currency;

            var currency = await player.GetCurrencyAsync();

            await player.SetCurrencyAsync(currency + message.Currency);
        }

        [PacketHandler]
        public async Task PickupItemHandler(PickupItemMessage message, Player player)
        {
            if (message.Loot == default)
            {
                Logger.Error($"{player} is trying to pick up invalid item.");
                return;
            }
            
            await player.OnLootPickup.InvokeAsync(message.Loot.Lot);
            
            await Object.DestroyAsync(message.Loot);

            await player.GetComponent<InventoryManagerComponent>().AddItemAsync(message.Loot.Lot, 1);
        }

        [PacketHandler]
        public async Task HasBeenCollectedHandler(HasBeenCollectedMessage message, Player player)
        {
            await player.GetComponent<MissionInventoryComponent>().CollectAsync(
                message.Associate
            );
        }
    }
}