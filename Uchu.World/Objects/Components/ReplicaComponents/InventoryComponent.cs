using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Microsoft.EntityFrameworkCore;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    public class InventoryComponent : ReplicaComponent
    {
        public override ComponentId Id => ComponentId.InventoryComponent;
        
        public Dictionary<EquipLocation, EquippedItem> Items { get; }
        
        public AsyncEvent<Item> OnEquipped { get; }
        
        public AsyncEvent<Item> OnUnEquipped { get; }

        protected InventoryComponent()
        {
            OnEquipped = new AsyncEvent<Item>();
            
            Items = new Dictionary<EquipLocation, EquippedItem>();
            
            OnUnEquipped = new AsyncEvent<Item>();
            
            Listen(OnStart, async () =>
            {
                if (GameObject is Player) return;
                
                await using var ctx = new CdClientContext();

                var component = await ctx.ComponentsRegistryTable.FirstOrDefaultAsync(
                    c => c.Id == GameObject.Lot && c.Componenttype == (int) ComponentId.InventoryComponent
                );

                var items = await ctx.InventoryComponentTable.Where(
                    i => i.Id == component.Componentid
                ).ToArrayAsync();

                foreach (var item in items)
                {
                    if (item.Itemid == default) continue;
                    
                    var lot = (Lot) item.Itemid;

                    var componentId = await lot.GetComponentIdAsync(ComponentId.ItemComponent);

                    var info = await ctx.ItemComponentTable.FirstAsync(i => i.Id == componentId);
                    
                    var location = (EquipLocation) info.EquipLocation;
                    
                    Items[location] = new EquippedItem
                    {
                        Id = ObjectId.Standalone,
                        Lot = lot
                    };
                }
            });
            
            Listen(OnDestroyed, () =>
            {
                OnEquipped.Clear();
                OnUnEquipped.Clear();
                
                return Task.CompletedTask;
            });

        }

        private async Task UpdateSlotAsync(EquipLocation slot, EquippedItem item)
        {
            if (Items.TryGetValue(slot, out var previous))
            {
                var id = await FindRootAsync(previous.Id);

                await UnEquipAsync(id);
            }

            Items[slot] = item;
        }

        private EquipLocation FindSlot(ObjectId id)
        {
            var reference = Items.FirstOrDefault(i => i.Value.Id == id);

            return reference.Key;
        }

        public async Task EquipAsync(EquippedItem item)
        {
            await using var cdClient = new CdClientContext();

            var componentId = await item.Lot.GetComponentIdAsync(ComponentId.ItemComponent);

            var info = await cdClient.ItemComponentTable.FirstAsync(i => i.Id == componentId);
            
            var location = (EquipLocation) info.EquipLocation;

            await UpdateSlotAsync(location, item);

            var skills = GameObject.TryGetComponent<SkillComponent>(out var skillComponent);

            if (skills)
            {
                await skillComponent.MountItemAsync(item.Lot);
            }

            await UpdateEquipState(item.Id, true);

            var proxies = await GenerateProxiesAsync(item.Id);

            foreach (var proxy in proxies)
            {
                var instance = await proxy.FindItemAsync();

                var lot = (Lot) instance.Lot;
                
                componentId = await lot.GetComponentIdAsync(ComponentId.ItemComponent);

                info = await cdClient.ItemComponentTable.FirstAsync(i => i.Id == componentId);
            
                location = (EquipLocation) info.EquipLocation;
                
                await UpdateSlotAsync(location, new EquippedItem
                {
                    Id = proxy,
                    Lot = lot
                });

                await UpdateEquipState(proxy, true);
                
                if (skills)
                {
                    await skillComponent.MountItemAsync(lot);
                }
            }
        }

        private async Task UnEquipAsync(ObjectId id)
        {
            id = await FindRootAsync(id);

            var slot = FindSlot(id);

            var info = Items[slot];

            var skills = GameObject.TryGetComponent<SkillComponent>(out var skillComponent);
            
            if (skills)
            {
                await skillComponent.DismountItemAsync(info.Lot);
            }

            await UpdateEquipState(id, false);

            Items.Remove(slot);

            var proxies = await FindProxiesAsync(id);

            foreach (var proxy in proxies)
            {
                slot = FindSlot(proxy);

                info = Items[slot];
                
                if (skills)
                {
                    await skillComponent.DismountItemAsync(info.Lot);
                }

                Items.Remove(slot);

                await UpdateEquipState(proxy, false);
            }

            await ClearProxiesAsync(id);
        }

        public async Task<bool> EquipItemAsync(Item item, bool ignoreAllChecks = false)
        {
            var itemType = (ItemType) (item.ItemComponent.ItemType ?? (int) ItemType.Invalid);

            if (!ignoreAllChecks)
            {
                if (!GameObject.GetComponent<ModularBuilderComponent>().IsBuilding)
                {
                    if (itemType == ItemType.Model || itemType == ItemType.LootModel || itemType == ItemType.Vehicle || item.Lot == 6086)
                    {
                        return false;
                    }
                }
            }
            
            await OnEquipped.InvokeAsync(item);

            await MountItemAsync(item.Id);

            GameObject.Serialize(GameObject);

            return true;
        }

        public async Task UnEquipItemAsync(Item item)
        {
            await OnUnEquipped.InvokeAsync(item);
            
            if (item?.Id <= 0) return;

            if (item != null)
            {
                await UnMountItemAsync(item.Id);
            }

            GameObject.Serialize(GameObject);
        }

        private static async Task<Lot[]> ParseProxyItemsAsync(Lot item)
        {
            await using var ctx = new CdClientContext();

            var componentId = await item.GetComponentIdAsync(ComponentId.ItemComponent);
            
            var itemInfo = await ctx.ItemComponentTable.FirstOrDefaultAsync(
                i => i.Id == componentId
            );

            if (itemInfo == default) return new Lot[0];

            if (string.IsNullOrWhiteSpace(itemInfo.SubItems)) return new Lot[0];
            
            var proxies = itemInfo.SubItems
                .Replace(" ", "")
                .Split(',')
                .Select(i => (Lot) int.Parse(i));
            
            return proxies.ToArray();
        }

        private static async Task<ObjectId> FindRootAsync(ObjectId id)
        {
            var item = await id.FindItemAsync();

            if (item == default) return ObjectId.Invalid;

            if (item.ParentId == ObjectId.Invalid) return id;

            return item.ParentId;
        }

        private static async Task<ObjectId[]> GenerateProxiesAsync(ObjectId id)
        {
            var item = await id.FindItemAsync();

            if (item == default) return new ObjectId[0];

            var proxies = await ParseProxyItemsAsync(item.Lot);
            
            await using var ctx = new UchuContext();

            var references = new ObjectId[proxies.Length];

            for (var index = 0; index < proxies.Length; index++)
            {
                var proxy = proxies[index];
                
                var instance = await ctx.InventoryItems.FirstOrDefaultAsync(
                    i => i.ParentId == id && i.Lot == proxy
                ).ConfigureAwait(false);

                if (instance == default)
                {
                    instance = new InventoryItem
                    {
                        Id = ObjectId.Standalone,
                        Lot = proxy,
                        Count = 0,
                        Slot = -1,
                        InventoryType = (int) InventoryType.Hidden,
                        CharacterId = item.CharacterId,
                        ParentId = id
                    };

                    await ctx.InventoryItems.AddAsync(instance);

                    await ctx.SaveChangesAsync();
                }

                references[index] = instance.Id;
            }

            return references;
        }

        private static async Task<ObjectId[]> FindProxiesAsync(ObjectId id)
        {
            await using var ctx = new UchuContext();

            var proxies = await ctx.InventoryItems.Where(
                i => i.ParentId == id
            ).ToArrayAsync().ConfigureAwait(false);

            return proxies.Select(i => (ObjectId) i.Id).ToArray();
        }

        private static async Task ClearProxiesAsync(ObjectId id)
        {
            await using var ctx = new UchuContext();

            var proxies = await ctx.InventoryItems.Where(
                i => i.ParentId == id
            ).ToArrayAsync().ConfigureAwait(false);

            foreach (var proxy in proxies)
            {
                ctx.InventoryItems.Remove(proxy);
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task UpdateEquipState(ObjectId id, bool state)
        {
            await using var ctx = new UchuContext();

            var item = await ctx.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);

            item.IsEquipped = state;

            await ctx.SaveChangesAsync();
        }

        private async Task MountItemAsync(ObjectId id)
        {
            var root = await id.FindItemAsync();
            
            await EquipAsync(new EquippedItem
            {
                Id = id,
                Lot = root.Lot
            });
        }

        private async Task UnMountItemAsync(ObjectId id)
        {
            var root = await FindRootAsync(id);

            await UnEquipAsync(root);
        }

        public override void Construct(BitWriter writer)
        {
            Serialize(writer);
        }

        public override void Serialize(BitWriter writer)
        {
            writer.WriteBit(true);

            var items = Items.Values.ToArray();

            writer.Write((uint) items.Length);

            foreach (var item in items)
            {
                writer.Write(item.Id);
                writer.Write(item.Lot);

                writer.WriteBit(false);

                writer.WriteBit(false);

                writer.WriteBit(false);

                writer.WriteBit(false);

                var info = item.Id.FindItem();

                if (info == default)
                {
                    writer.WriteBit(false);
                }
                else
                {
                    if (writer.Flag(!string.IsNullOrWhiteSpace(info.ExtraInfo)))
                    {
                        writer.WriteLdfCompressed(LegoDataDictionary.FromString(info.ExtraInfo));
                    }
                }

                writer.WriteBit(true);
            }

            writer.WriteBit(false);
        }
    }
}