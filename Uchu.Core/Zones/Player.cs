using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchu.Core.Collections;
using Uchu.World;

namespace Uchu.Core
{
    public class Player
    {
        public readonly IPEndPoint EndPoint;
        private readonly Server _server;

        public World World { get; set; }
        public long CharacterId { get; set; }

        public ReplicaPacket ReplicaPacket => World.GetObject(CharacterId);

        public Player(Server server, IPEndPoint endPoint)
        {
            _server = server;
            EndPoint = endPoint;

            var session = server.SessionCache.GetSession(endPoint);

            CharacterId = session.CharacterId;
            World = server.Worlds[(ZoneId) session.ZoneId];
        }

        public async Task UnequipItemAsync(long objectId)
        {
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                var item = character.Items.Find(i => i.InventoryItemId == objectId);

                if (item == null)
                    return;

                item.IsEquipped = false;

                await ctx.SaveChangesAsync();

                var obj = World.GetObject(CharacterId);
                var comp = (InventoryComponent) obj.Components.First(c => c is InventoryComponent);

                var items = comp.Items.ToList();

                items.Remove(items.Find(i => i.LOT == item.LOT));

                comp.Items = items.ToArray();

                World.UpdateObject(obj);
            }
        }

        public async Task EquipItemAsync(long objectId)
        {
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                var item = character.Items.Find(i => i.InventoryItemId == objectId);

                if (item == null)
                    return;

                item.IsEquipped = true;

                await ctx.SaveChangesAsync();

                var obj = World.GetObject(CharacterId);
                var comp = (InventoryComponent) obj.Components.First(c => c is InventoryComponent);

                var items = comp.Items.ToList();

                items.Add(item);

                comp.Items = items.ToArray();

                World.UpdateObject(obj);
            }
        }

        public async Task RemoveItemAsync(int lot, int count = 1)
        {
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                var item = character.Items.Find(i => i.LOT == lot);

                if (item.Count - count > 0)
                {
                    item.Count -= count;
                }
                else
                {
                    character.Items.Remove(item);
                }

                await ctx.SaveChangesAsync();
            }
        }

        public async Task AddItemAsync(int lot, int count = 1, LegoDataDictionary extraInfo = null)
        {
            var comp = await _server.CDClient.GetComponentIdAsync(lot, 11);
            var itemComp = await _server.CDClient.GetItemComponentAsync((int) comp);

            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                var id = Utils.GenerateObjectId();
                int inventoryType;
                try
                {
                    inventoryType = (int) Utils.GetItemInventoryType((ItemType) itemComp.ItemType);
                }
                catch
                {
                    StatPickup(lot);
                    return;
                }

                var items = character.Items.Where(i => i.InventoryType == inventoryType).ToArray();

                Console.WriteLine($"Adding {lot} to inventory!");
                
                var slot = 0;

                if (items.Length > 0)
                {
                    var max = items.Max(i => i.Slot);
                    slot = max + 1;

                    for (var i = 0; i < max; i++)
                    {
                        if (items.All(itm => itm.Slot != i))
                            slot = i;
                    }
                }

                var item = new InventoryItem
                {
                    InventoryItemId = id,
                    LOT = lot,
                    Slot = slot,
                    Count = count,
                    InventoryType = inventoryType
                };

                if (extraInfo != null)
                    item.ExtraInfo = extraInfo.ToString();

                character.Items.Add(item);

                await ctx.SaveChangesAsync();

                _server.Send(new AddItemToInventoryMessage
                {
                    ObjectId = CharacterId,
                    ItemLOT = lot,
                    ItemCount = (uint) item.Count,
                    ItemObjectId = id,
                    Slot = item.Slot,
                    InventoryType = inventoryType,
                    ExtraInfo = extraInfo
                }, EndPoint);

                await UpdateTaskAsync(lot, MissionTaskType.ObtainItem);
            }
        }

        /// <summary>
        ///     Pickup of a stat pickup.
        /// </summary>
        /// <param name="lot"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void StatPickup(int lot)
        {
            Console.WriteLine($"Updating stat for LOT: {lot}");
            var imaginationToAdd = 0;
            var armorToAdd = 0;
            var healthToAdd = 0;
            
            switch ((PickupLOT) lot)
            {
                case PickupLOT.Imagination:
                    imaginationToAdd = 1;
                    break;
                case PickupLOT.TwoImagination:
                    imaginationToAdd = 2;
                    break;
                case PickupLOT.ThreeImagination:
                    imaginationToAdd = 3;
                    break;
                case PickupLOT.FiveImagination:
                    imaginationToAdd = 5;
                    break;
                case PickupLOT.TenImagination:
                    imaginationToAdd = 10;
                    break;
                case PickupLOT.Health:
                    healthToAdd = 1;
                    break;
                case PickupLOT.TwoHealth:
                    healthToAdd = 2;
                    break;
                case PickupLOT.ThreeHealth:
                    healthToAdd = 3;
                    break;
                case PickupLOT.FiveHealth:
                    healthToAdd = 5;
                    break;
                case PickupLOT.TenHealth:
                    healthToAdd = 10;
                    break;
                case PickupLOT.Armor:
                    armorToAdd = 1;
                    break;
                case PickupLOT.TwoArmor:
                    armorToAdd = 2;
                    break;
                case PickupLOT.ThreeArmor:
                    armorToAdd = 3;
                    break;
                case PickupLOT.FiveArmor:
                    armorToAdd = 5;
                    break;
                case PickupLOT.TenArmor:
                    armorToAdd = 10;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lot), lot, null);
            }

            using (var ctx = new UchuContext())
            {
                var character = ctx.Characters.First(c => c.CharacterId == CharacterId);
                character.CurrentImagination += imaginationToAdd;
                character.CurrentHealth += healthToAdd;
                character.CurrentArmor += armorToAdd;
                
                ctx.SaveChanges();
            }
            
            Console.WriteLine($"Adding: {imaginationToAdd} | {healthToAdd} | {armorToAdd}");
            
            UpdateStats();
        }

        public void UpdateStats()
        {
            using (var ctx = new UchuContext())
            {
                var character = ctx.Characters.First(c => c.CharacterId == CharacterId);
                var obj = World.GetObject(CharacterId);
                var stats = (StatsComponent) obj.Components.First(c => c is StatsComponent);

                if (character.CurrentImagination > character.MaximumImagination)
                    character.CurrentImagination = character.MaximumImagination;
                if (character.CurrentHealth > character.MaximumHealth)
                    character.CurrentHealth = character.MaximumHealth;
                if (character.CurrentArmor > character.MaximumArmor)
                    character.CurrentArmor = character.MaximumArmor;
                
                stats.CurrentImagination = (uint) character.CurrentImagination;
                stats.CurrentHealth = (uint) character.CurrentHealth;
                stats.CurrentArmor = (uint) character.CurrentArmor;

                stats.MaxImagination = character.MaximumImagination;
                stats.MaxHealth = character.MaximumHealth;
                stats.MaxArmor = character.MaximumArmor;
                
                World.UpdateObject(obj);
            }
        }
        
        public static async Task MoveItemAsync(long item, ulong slot)
        {
            Console.WriteLine($"Moving {item} to {slot}.");
            using (var ctx = new UchuContext())
            {
                ctx.InventoryItems.First(i => i.InventoryItemId == item).Slot = (int) slot;
                await ctx.SaveChangesAsync();
            }
        }

        public async Task UpdateTaskAsync(int id, MissionTaskType type = MissionTaskType.None)
        {
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Missions).ThenInclude(t => t.Tasks)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                foreach (var mission in character.Missions)
                {
                    if (mission.State != (int) MissionState.Active &&
                        mission.State != (int) MissionState.CompletedActive)
                        continue;

                    var tasks = await _server.CDClient.GetMissionTasksAsync(mission.MissionId);

                    var task = tasks.Find(
                        t => t.Targets.Contains(id) && mission.Tasks.Exists(a => a.TaskId == t.UId));

                    if (task == null)
                        continue;

                    var charTask = mission.Tasks.Find(t => t.TaskId == task.UId);

                    if (!charTask.Values.Contains(id))
                        charTask.Values.Add(id);

                    _server.Send(new NotifyMissionTaskMessage
                    {
                        ObjectId = CharacterId,
                        MissionId = task.MissionId,
                        TaskIndex = tasks.IndexOf(task),
                        Updates = new[] {(float) charTask.Values.Count}
                    }, EndPoint);

                    await ctx.SaveChangesAsync();
                }

                var otherTasks = await _server.CDClient.GetMissionTasksWithTargetAsync(id);

                foreach (var task in otherTasks)
                {
                    var mission = await _server.CDClient.GetMissionAsync(task.MissionId);

                    if (mission.OffererObjectId != -1 || mission.TargetObjectId != -1 || mission.IsMission ||
                        task.TaskType != (int) type)
                        continue;

                    var tasks = await _server.CDClient.GetMissionTasksAsync(mission.MissionId);

                    if (!character.Missions.Exists(m => m.MissionId == mission.MissionId))
                    {
                        var canOffer = true;

                        foreach (var mId in mission.PrerequiredMissions)
                        {
                            if (!character.Missions.Exists(m => m.MissionId == mId))
                            {
                                canOffer = false;
                                break;
                            }

                            var chrMission = character.Missions.Find(m => m.MissionId == mId);

                            if (!await AllTasksCompletedAsync(chrMission))
                            {
                                canOffer = false;
                                break;
                            }
                        }

                        if (!canOffer)
                            continue;

                        character.Missions.Add(new Mission
                        {
                            MissionId = mission.MissionId,
                            State = (int) MissionState.Active,
                            Tasks = tasks.Select(t => new MissionTask
                            {
                                TaskId = t.UId,
                                Values = new List<float>()
                            }).ToList()
                        });
                    }

                    var charMission = character.Missions.Find(m => m.MissionId == mission.MissionId);

                    if (charMission.State != (int) MissionState.Active ||
                        charMission.State != (int) MissionState.CompletedActive)
                        continue;

                    var charTask = charMission.Tasks.Find(t => t.TaskId == task.UId);

                    if (!charTask.Values.Contains(id))
                        charTask.Values.Add(id);

                    await ctx.SaveChangesAsync();

                    _server.Send(new NotifyMissionTaskMessage
                    {
                        ObjectId = CharacterId,
                        MissionId = mission.MissionId,
                        TaskIndex = tasks.IndexOf(task),
                        Updates = new[] {(float) charTask.Values.Count}
                    }, EndPoint);

                    if (await AllTasksCompletedAsync(charMission))
                        await CompleteMissionAsync(mission);
                }
            }
        }

        public async Task UpdateObjectTaskAsync(MissionTaskType type, long objectId)
        {
            var obj = World.GetObject(objectId);

            if (obj == null)
                return;

            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Missions).ThenInclude(t => t.Tasks)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                foreach (var mission in character.Missions)
                {
                    if (mission.State != (int) MissionState.Active &&
                        mission.State != (int) MissionState.CompletedActive)
                        continue;

                    var tasks = await _server.CDClient.GetMissionTasksAsync(mission.MissionId);

                    var task = tasks.Find(t =>
                        t.Targets.Contains(obj.LOT) && mission.Tasks.Exists(a => a.TaskId == t.UId));

                    if (task == null)
                        continue;

                    var charTask = mission.Tasks.Find(t => t.TaskId == task.UId);

                    switch (type)
                    {
                        case MissionTaskType.Interact:
                            if (!charTask.Values.Contains(obj.LOT))
                                charTask.Values.Add(obj.LOT);

                            _server.Send(new NotifyMissionTaskMessage
                            {
                                ObjectId = CharacterId,
                                MissionId = task.MissionId,
                                TaskIndex = tasks.IndexOf(task),
                                Updates = new[] {(float) charTask.Values.Count}
                            }, EndPoint);

                            await ctx.SaveChangesAsync();
                            break;
                        case MissionTaskType.Collect:
                            var component = (CollectibleComponent) obj.Components.First(c => c is CollectibleComponent);

                            if (!charTask.Values.Contains(component.CollectibleId))
                                charTask.Values.Add(component.CollectibleId);

                            _server.Send(new NotifyMissionTaskMessage
                            {
                                ObjectId = CharacterId,
                                MissionId = task.MissionId,
                                TaskIndex = tasks.IndexOf(task),
                                Updates = new[] {(float) (component.CollectibleId + (World.ZoneId << 8))}
                            }, EndPoint);

                            await ctx.SaveChangesAsync();
                            break;
                        case MissionTaskType.KillEnemy:
                            break;
                        case MissionTaskType.Script:
                            break;
                        case MissionTaskType.QuickBuild:
                            break;
                        case MissionTaskType.GoToNPC:
                            break;
                        case MissionTaskType.UseEmote:
                            break;
                        case MissionTaskType.UseConsumable:
                            break;
                        case MissionTaskType.UseSkill:
                            break;
                        case MissionTaskType.ObtainItem:
                            break;
                        case MissionTaskType.Discover:
                            break;
                        case MissionTaskType.None:
                            break;
                        case MissionTaskType.MinigameAchievement:
                            break;
                        case MissionTaskType.MissionComplete:
                            break;
                        case MissionTaskType.TamePet:
                            break;
                        case MissionTaskType.Racing:
                            break;
                        case MissionTaskType.Flag:
                            break;
                        case MissionTaskType.NexusTowerBrickDonation:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(type), type, null);
                    }
                }

                var otherTasks = await _server.CDClient.GetMissionTasksWithTargetAsync(obj.LOT);

                foreach (var task in otherTasks)
                {
                    var mission = await _server.CDClient.GetMissionAsync(task.MissionId);

                    if (mission.OffererObjectId != -1 || mission.TargetObjectId != -1 || mission.IsMission ||
                        task.TaskType != (int) type)
                        continue;

                    var tasks = await _server.CDClient.GetMissionTasksAsync(mission.MissionId);

                    if (!character.Missions.Exists(m => m.MissionId == mission.MissionId))
                    {
                        var canOffer = true;

                        foreach (var id in mission.PrerequiredMissions)
                        {
                            if (!character.Missions.Exists(m => m.MissionId == id))
                            {
                                canOffer = false;
                                break;
                            }

                            var chrMission = character.Missions.Find(m => m.MissionId == id);

                            if (!await AllTasksCompletedAsync(chrMission))
                            {
                                canOffer = false;
                                break;
                            }
                        }

                        if (!canOffer)
                            continue;

                        character.Missions.Add(new Mission
                        {
                            MissionId = mission.MissionId,
                            State = (int) MissionState.Active,
                            Tasks = tasks.Select(t => new MissionTask
                            {
                                TaskId = t.UId,
                                Values = new List<float>()
                            }).ToList()
                        });
                    }

                    var charMission = character.Missions.Find(m => m.MissionId == mission.MissionId);

                    if (charMission.State != (int) MissionState.Active ||
                        charMission.State != (int) MissionState.CompletedActive)
                        continue;

                    var charTask = charMission.Tasks.Find(t => t.TaskId == task.UId);

                    if (!charTask.Values.Contains(obj.LOT))
                        charTask.Values.Add(obj.LOT);

                    await ctx.SaveChangesAsync();

                    _server.Send(new NotifyMissionTaskMessage
                    {
                        ObjectId = CharacterId,
                        MissionId = mission.MissionId,
                        TaskIndex = tasks.IndexOf(task),
                        Updates = new[] {(float) charTask.Values.Count}
                    }, EndPoint);

                    if (await AllTasksCompletedAsync(charMission))
                        await CompleteMissionAsync(mission);
                }
            }
        }

        public async Task CompleteMissionAsync(MissionsRow mission)
        {
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items).Include(c => c.Missions)
                    .ThenInclude(m => m.Tasks).SingleAsync(c => c.CharacterId == CharacterId);

                if (!character.Missions.Exists(m => m.MissionId == mission.MissionId))
                {
                    var tasks = await _server.CDClient.GetMissionTasksAsync(mission.MissionId);

                    character.Missions.Add(new Mission
                    {
                        MissionId = mission.MissionId,
                        State = (int) MissionState.Active,
                        Tasks = tasks.Select(t => new MissionTask
                        {
                            TaskId = t.UId,
                            Values = t.Targets.Where(tgt => tgt is int).Select(tgt => (float) (int) tgt).ToList()
                        }).ToList()
                    });
                }

                var charMission = character.Missions.Find(m => m.MissionId == mission.MissionId);

                _server.Send(new NotifyMissionMessage
                {
                    ObjectId = CharacterId,
                    MissionId = mission.MissionId,
                    MissionState = MissionState.Unavailable,
                    SendingRewards = true
                }, EndPoint);

                charMission.State = (int) MissionState.Completed;
                charMission.CompletionCount++;
                charMission.LastCompletion = DateTimeOffset.Now.ToUnixTimeSeconds();

                if (character.MaximumImagination == 0 && mission.MaximumImaginationReward > 0)
                {
                    // Bob mission
                    await CompleteMissionAsync(await _server.CDClient.GetMissionAsync(664));
                    _server.Send(new RestoreToPostLoadStatsMessage {ObjectId = CharacterId}, EndPoint);
                }

                character.Currency += mission.CurrencyReward;
                character.UniverseScore += mission.LegoScoreReward;
                character.MaximumHealth += mission.MaximumHealthReward;
                character.MaximumImagination += mission.MaximumImaginationReward;

                if (mission.CurrencyReward > 0)
                {
                    _server.Send(new SetCurrencyMessage
                    {
                        ObjectId = CharacterId,
                        Currency = character.Currency,
                        Position = Vector3.Zero // TODO: find out what to set this to
                    }, EndPoint);
                }

                if (mission.LegoScoreReward > 0)
                {
                    _server.Send(new ModifyLegoScoreMessage
                    {
                        ObjectId = CharacterId,
                        SourceType = 2,
                        Score = mission.LegoScoreReward
                    }, EndPoint);
                }

                if (mission.MaximumImaginationReward > 0)
                {
                    var dict = new Dictionary<string, object>
                    {
                        ["amount"] = character.MaximumImagination.ToString(),
                        ["type"] = "imagination"
                    };

                    _server.Send(new UIMessageToClientMessage
                    {
                        ObjectId = CharacterId,
                        Arguments = new AMF3<object>(dict),
                        MessageName = "MaxPlayerBarUpdate"
                    }, EndPoint);
                }

                if (mission.MaximumHealthReward > 0)
                {
                    var dict = new Dictionary<string, object>
                    {
                        ["amount"] = character.MaximumHealth.ToString(),
                        ["type"] = "health"
                    };

                    _server.Send(new UIMessageToClientMessage
                    {
                        ObjectId = CharacterId,
                        Arguments = new AMF3<object>(dict),
                        MessageName = "MaxPlayerBarUpdate"
                    }, EndPoint);
                }

                if (mission.FirstItemReward != -1)
                    await AddItemAsync(mission.FirstItemReward, mission.FirstItemRewardCount);

                if (mission.SecondItemReward != -1)
                    await AddItemAsync(mission.SecondItemReward, mission.SecondItemRewardCount);

                if (mission.ThirdItemReward != -1)
                    await AddItemAsync(mission.ThirdItemReward, mission.ThirdItemRewardCount);

                if (mission.FourthItemReward != -1)
                    await AddItemAsync(mission.FourthItemReward, mission.FourthItemRewardCount);

                _server.Send(new NotifyMissionMessage
                {
                    ObjectId = CharacterId,
                    MissionId = mission.MissionId,
                    MissionState = MissionState.Completed,
                    SendingRewards = false
                }, EndPoint);

                await UpdateTaskAsync(mission.MissionId, MissionTaskType.MissionComplete);

                await ctx.SaveChangesAsync();
            }
        }

        public async Task<bool> AllTasksCompletedAsync(Mission mission)
        {
            var tasks = await _server.CDClient.GetMissionTasksAsync(mission.MissionId);

            return tasks.TrueForAll(t => mission.Tasks.Find(t2 => t2.TaskId == t.UId).Values.Count >= t.TargetValue);
        }

        public async Task OfferMissionAsync(long offererId)
        {
            var obj = World.GetObject(offererId);

            if (obj == null)
                return;

            var componentId = await _server.CDClient.GetComponentIdAsync(obj.LOT, 73);
            var missions = await _server.CDClient.GetNPCMissionsAsync((int) componentId);

            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Missions).ThenInclude(m => m.Tasks)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                foreach (var mission in missions)
                {
                    var miss = await _server.CDClient.GetMissionAsync(mission.MissionId);

                    if (!miss.IsMission)
                        continue;

                    if (mission.AcceptsMission)
                    {
                        if (character.Missions.Exists(m => m.MissionId == mission.MissionId))
                        {
                            var charMission = character.Missions.Find(m => m.MissionId == mission.MissionId);

                            if (charMission.State != (int) MissionState.Completed && await AllTasksCompletedAsync(charMission))
                            {
                                _server.Send(new OfferMissionMessage
                                {
                                    ObjectId = character.CharacterId,
                                    MissionId = mission.MissionId,
                                    OffererObjectId = offererId
                                }, EndPoint);

                                /*_server.Send(new OfferMissionMessage
                                {
                                    ObjectId = character.CharacterId,
                                    MissionId = mission.MissionId,
                                    OffererObjectId = offererId
                                }, _endpoint);*/

                                break;
                            }
                        }
                    }

                    if (mission.OffersMission)
                    {
                        if (!character.Missions.Exists(m => m.MissionId == mission.MissionId) ||
                            character.Missions.Find(m => m.MissionId == mission.MissionId).State ==
                            (int) MissionState.Active ||
                            character.Missions.Find(m => m.MissionId == mission.MissionId).State ==
                            (int) MissionState.ReadyToComplete)
                        {
                            var canOffer = true;

                            foreach (var id in miss.PrerequiredMissions)
                            {
                                if (!character.Missions.Exists(m => m.MissionId == id))
                                {
                                    canOffer = false;
                                    break;
                                }

                                var chrMission = character.Missions.Find(m => m.MissionId == id);

                                if (!await AllTasksCompletedAsync(chrMission))
                                {
                                    canOffer = false;
                                    break;
                                }
                            }

                            if (!canOffer)
                                continue;

                            _server.Send(new OfferMissionMessage
                            {
                                ObjectId = character.CharacterId,
                                MissionId = mission.MissionId,
                                OffererObjectId = offererId
                            }, EndPoint);

                            /*_server.Send(new OfferMissionMessage
                            {
                                ObjectId = character.CharacterId,
                                MissionId = mission.MissionId,
                                OffererObjectId = offererId
                            }, _endpoint);*/

                            break;
                        }
                    }
                }
            }
        }

        public async Task LaunchRocket(long objectId)
        {
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                var rocket = character.Items.Find(i => i.LOT == 6416); // TODO: find out how to properly get the active rocket

                _server.Send(new EquipItemMessage
                {
                    ObjectId = CharacterId,
                    ItemObjectId = rocket.InventoryItemId
                }, EndPoint);

                _server.Send(new ChangeObjectWorldStateMessage
                {
                    ObjectId = rocket.InventoryItemId,
                    State = ObjectWorldState.Attached
                }, EndPoint);

                _server.Send(new FireClientEventMessage
                {
                    ObjectId = objectId,
                    Arguments = "RocketEquipped",
                    TargetObjectId = rocket.InventoryItemId,
                    SenderObjectId = CharacterId
                }, EndPoint);

                character.LandingByRocket = true;
                character.Rocket =
                    ((LegoDataList) LegoDataDictionary.FromString(rocket.ExtraInfo)["assemblyPartLOTs"]).ToString(";") +
                    ";";

                await ctx.SaveChangesAsync();
            }
        }

        public async Task Smash()
        {
            Console.WriteLine("Smashing player...");
            using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.Include(c => c.Items)
                    .SingleAsync(c => c.CharacterId == CharacterId);

                _server.Send(new DieMessage
                {
                    ClientDeath = true,
                    DeathType = "electro-shock-death",
                    SpawnLoot = false,
                    LootOwner = CharacterId
                }, EndPoint);
            }
        }
    }
}