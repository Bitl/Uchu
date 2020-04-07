using System.Linq;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Uchu.Core;

namespace Uchu.World
{
    public class ModularBuilderComponent : Component
    {
        private bool _building;

        public GameObject BasePlate { get; private set; }

        protected ModularBuilderComponent()
        {
            Listen(OnStart, () =>
            {
                var inventory = GameObject.GetComponent<InventoryComponent>();

                Listen(inventory.OnEquipped, item =>
                {
                    Logger.Information($"Equipped {item.ItemType} item");
                    if (item.ItemType == ItemType.LootModel)
                    {
                        StartBuildingWithItem(item);
                    }
                    
                    return Task.CompletedTask;
                });
                
                return Task.CompletedTask;
            });
        }
        
        public bool IsBuilding
        {
            get => _building;
            private set
            {
                var player = (Player) GameObject;
                
                player.Message(new SetStunnedMessage
                {
                    Associate = GameObject,
                    CantAttack = value
                });

                _building = value;

                if (value) return;

                Task.Run(ConfirmFinish);
            }
        }

        public void StartBuilding(StartBuildingWithItemMessage message)
        {
            IsBuilding = true;
            
            BasePlate = message.Associate;

            var player = (Player) GameObject;
            
            player.Message(new StartArrangingWithItemMessage
            {
                Associate = GameObject,
                FirstTime = message.FirstTime,
                BuildArea = message.Associate,
                StartPosition = Transform.Position,
                
                SourceBag = message.SourceBag,
                Source = message.Source,
                SourceLot = message.SourceLot,
                SourceType = 8,
                
                Target = message.Target,
                TargetLot = message.TargetLot,
                TargetPosition = message.TargetPosition,
                TargetType = message.TargetType
            });
        }

        public void StartBuildingWithItem(Item item)
        {
            var player = (Player) GameObject;

            player.Message(new StartArrangingWithItemMessage
            {
                Associate = GameObject,
                FirstTime = false,
                BuildArea = BasePlate,
                StartPosition = Transform.Position,
                
                SourceBag = (int) item.Inventory.InventoryType,
                Source = item,
                SourceLot = item.Lot,
                SourceType = 8, // TODO: find out how to get this
                
                Target = BasePlate,
                TargetLot = BasePlate.Lot,
                TargetPosition = BasePlate.Transform.Position,
                TargetType = 0
            });
        }
        
        public async Task FinishBuilding(Lot[] models)
        {
            var inventory = GameObject.GetComponent<InventoryManagerComponent>();

            foreach (var module in models)
            {
                await inventory.RemoveItemAsync(module, 1, InventoryType.TemporaryModels);
            }

            var model = new LegoDataDictionary
            {
                ["assemblyPartLOTs"] = LegoDataList.FromEnumerable(models.Select(s => s.Id))
            };
            
            await inventory.AddItemAsync(6416, 1, InventoryType.Models, model);

            await ConfirmFinish();
        }
        
        public void DoneArranging(DoneArrangingWithItemMessage message)
        {
            
        }

        public async Task Pickup(Lot lot)
        {
            var inventory = GameObject.GetComponent<InventoryManagerComponent>();
            
            var item = inventory[InventoryType.TemporaryModels].Items.First(i => i.Lot == lot);

            await item.EquipAsync();
            
            /*
            As<Player>().Message(new StartArrangingWithItemMessage
            {
                Associate = GameObject,
                FirstTime = false
            });
            */
        }

        public async Task ConfirmFinish()
        {
            if (!IsBuilding) return;

            var inventory = GameObject.GetComponent<InventoryManagerComponent>();
            
            foreach (var temp in inventory[InventoryType.TemporaryModels].Items)
            {
                await inventory.MoveItemsBetweenInventoriesAsync(
                    temp,
                    temp.Lot,
                    await temp.GetCountAsync(),
                    InventoryType.TemporaryModels,
                    InventoryType.Models
                );
            }
            
            var thinkingHat = inventory[InventoryType.Items].Items.First(i => i.Lot == 6086);

            await thinkingHat.UnEquipAsync();
            
            IsBuilding = false;
        }
    }
}