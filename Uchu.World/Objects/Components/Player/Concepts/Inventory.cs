using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchu.Core;

namespace Uchu.World
{
    public class Inventory
    {
        private readonly List<Item> _items;
        
        public InventoryType InventoryType { get; }
        
        public InventoryManagerComponent ManagerComponent { get; }

        private int _size;
        
        public int Size
        {
            get
            {
                if (InventoryType == InventoryType.Items)
                {
                    using var ctx = new UchuContext();

                    var character = ctx.Characters.First(
                        c => c.Id == ManagerComponent.GameObject.Id
                    );

                    return character.InventorySize;
                }

                return _size;
            }
            set
            {
                if (InventoryType == InventoryType.Items)
                {
                    using var ctx = new UchuContext();

                    var character = ctx.Characters.First(
                        c => c.Id == ManagerComponent.GameObject.Id
                    );

                    character.InventorySize = value;

                    ctx.SaveChanges();
                }
                
                ((Player) ManagerComponent.GameObject).Message(new SetInventorySizeMessage
                {
                    Associate = ManagerComponent.GameObject,
                    InventoryType = InventoryType,
                    Size = value
                });

                _size = value;
            }
        }

        internal Inventory(InventoryType inventoryType, InventoryManagerComponent managerComponent)
        {
            InventoryType = inventoryType;
            ManagerComponent = managerComponent;
            
            _items = new List<Item>();
        }

        public async Task CollectItemsAsync()
        {
            await using var ctx = new UchuContext();

            var playerCharacter = await ctx.Characters.Include(c => c.Items).FirstAsync(
                c => c.Id == ManagerComponent.GameObject.Id
            );

            var inventoryItems = playerCharacter.Items.Where(
                item => item.ParentId == ObjectId.Invalid && (InventoryType) item.InventoryType == InventoryType
            ).ToList();

            foreach (var item in inventoryItems)
            {
                if (item == default) continue;
                
                var instance = await Item.InstantiateAsync(item.Id, this);

                _items.Add(instance);
            }
            
            Size = InventoryType != InventoryType.Items ? 1000 : playerCharacter.InventorySize;
            
            foreach (var item in _items)
            {
                await Object.StartAsync(item);
            }
        }

        public IEnumerable<Item> Items => Array.AsReadOnly(_items.ToArray());

        public Item this[long id] => Items.FirstOrDefault(i => i.Id == id);

        public void ManageItem(Item item)
        {
            _items.Add(item);
        }
        
        public void UnManageItem(Item item)
        {
            _items.Remove(item);
        }
    }
}