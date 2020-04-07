using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchu.Core;
using Uchu.Core.Client;
using Uchu.Core.Profiling;
using Uchu.World.Client;
using Uchu.World.MissionSystem;

namespace Uchu.World
{
    public class MissionInventoryComponent : Component
    {
        private static MissionTasks[] TasksCache { get; set; }
        
        private static Missions[] MissionCache { get; set; }
        
        public List<MissionInstance> MissionInstances { get; private set; }

        public Mission[] GetCompletedMissions()
        {
            using var ctx = new UchuContext();
            return ctx.Missions.Include(m => m.Tasks).ThenInclude(m => m.Values).Where(
                m => m.Character.Id == GameObject.Id && m.CompletionCount > 0
            ).ToArray();
        }

        public Mission[] GetActiveMissions()
        {
            using var ctx = new UchuContext();
            return ctx.Missions.Include(m => m.Tasks).ThenInclude(m => m.Values).Where(
                m => m.Character.Id == GameObject.Id &&
                     m.State == (int) MissionState.Active ||
                     m.State == (int) MissionState.CompletedActive
            ).ToArray();
        }

        public Mission[] GetMissions()
        {
            using var ctx = new UchuContext();
            return ctx.Missions.Include(m => m.Tasks).ThenInclude(m => m.Values).Where(
                m => m.Character.Id == GameObject.Id
            ).ToArray();
        }

        public void MessageOfferMission(int missionId, GameObject missionGiver)
        {
            var player = (Player) GameObject;
            
            player.Message(new OfferMissionMessage
            {
                Associate = GameObject,
                MissionId = missionId,
                QuestGiver = missionGiver
            });
            
            player.Message(new OfferMissionMessage
            {
                Associate = missionGiver,
                MissionId = missionId,
                QuestGiver = missionGiver
            });
        }

        public MissionInventoryComponent()
        {
            Listen(OnStart, async () =>
            {
                await LoadAsync();
            });
        }

        public async Task LoadAsync()
        {
            await using var ctx = new UchuContext();

            var missions = await ctx.Missions.Where(
                m => m.CharacterId == GameObject.Id
            ).ToArrayAsync();

            MissionInstances = new List<MissionInstance>();

            foreach (var mission in missions)
            {
                var instance = new MissionInstance(GameObject as Player, mission.MissionId);
                
                MissionInstances.Add(instance);

                await instance.LoadAsync();
            }
        }
        
        public async Task RespondToMissionAsync(int missionId, GameObject missionGiver, Lot rewardItem)
        {
            //
            // The player has clicked on the accept or complete button.
            //

            await using var ctx = new UchuContext();
            await using var cdClient = new CdClientContext();

            //
            // Get the mission the player is responding to.
            //

            var mission = MissionInstances.FirstOrDefault(m => m.MissionId == missionId);

            //
            // Check if the player is accepting a mission or responding to one.
            //

            if (mission == default)
            {
                var instance = new MissionInstance(GameObject as Player, missionId);

                MissionInstances.Add(instance);

                await instance.LoadAsync();

                await instance.StartAsync();
                
                return;
            }

            //
            // Player is responding to an active mission.
            //

            var isComplete = await mission.IsCompleteAsync();
            
            if (!isComplete)
            {
                //
                // Mission is not complete.
                //

                var currentState = await mission.GetMissionStateAsync();

                await mission.UpdateMissionStateAsync(currentState);

                MessageOfferMission(missionId, missionGiver);

                return;
            }

            //
            // Complete mission.
            //

            await mission.CompleteAsync(rewardItem);

            missionGiver?.GetComponent<MissionGiverComponent>().OfferMission(GameObject as Player);
        }

        public async Task CompleteMissionAsync(int missionId)
        {
            //
            // Get the mission the player is responding to.
            //

            var mission = MissionInstances.FirstOrDefault(m => m.MissionId == missionId);

            //
            // Check if the player is accepting a mission or responding to one.
            //

            if (mission == default)
            {
                var instance = new MissionInstance(GameObject as Player, missionId);
                
                MissionInstances.Add(instance);
                
                await instance.LoadAsync();

                await instance.CompleteAsync();
                
                return;
            }

            await mission.CompleteAsync();
        }

        public async Task<T[]> FindActiveTasksAsync<T>() where T : MissionTaskBase
        {
            var tasks = new List<T>();

            foreach (var instance in MissionInstances)
            {
                var state = await instance.GetMissionStateAsync();
                
                if (state != MissionState.Active && state != MissionState.CompletedActive) continue;

                foreach (var task in instance.Tasks.OfType<T>())
                {
                    var isComplete = await task.IsCompleteAsync();
                    
                    if (isComplete) continue;

                    tasks.Add(task);
                }
            }

            return tasks.ToArray();
        }

        public async Task SmashAsync(Lot lot)
        {
            foreach (var task in await FindActiveTasksAsync<SmashTask>())
            {
                await task.Progress(lot);
            }

            await SearchForNewAchievementsAsync<SmashTask>(MissionTaskType.Smash, lot, async task =>
            {
                await task.Progress(lot);
            });
        }

        public async Task CollectAsync(GameObject gameObject)
        {
            foreach (var task in await FindActiveTasksAsync<CollectTask>())
            {
                await task.Progress(gameObject);
            }

            await SearchForNewAchievementsAsync<CollectTask>(MissionTaskType.Collect, gameObject.Lot, async task =>
            {
                await task.Progress(gameObject);
            });
        }

        public async Task ScriptAsync(int id)
        {
            foreach (var task in await FindActiveTasksAsync<ScriptTask>())
            {
                await task.Progress(id);
            }

            await SearchForNewAchievementsAsync<ScriptTask>(MissionTaskType.Script, id, async task =>
            {
                await task.Progress(id);
            });
        }

        public async Task QuickBuildAsync(Lot lot)
        {
            foreach (var task in await FindActiveTasksAsync<QuickBuildTask>())
            {
                await task.Progress(lot);
            }

            await SearchForNewAchievementsAsync<QuickBuildTask>(MissionTaskType.QuickBuild, lot, async task =>
            {
                await task.Progress(lot);
            });
        }

        public async Task GoToNpcAsync(Lot lot)
        {
            foreach (var task in await FindActiveTasksAsync<GoToNpcTask>())
            {
                await task.Progress(lot);
            }

            await SearchForNewAchievementsAsync<GoToNpcTask>(MissionTaskType.GoToNpc, lot, async task =>
            {
                await task.Progress(lot);
            });
        }

        public async Task UseEmoteAsync(GameObject gameObject, int emote)
        {
            foreach (var task in await FindActiveTasksAsync<UseEmoteTask>())
            {
                await task.Progress(gameObject, emote);
            }

            await SearchForNewAchievementsAsync<UseEmoteTask>(MissionTaskType.UseEmote, emote, async task =>
            {
                await task.Progress(gameObject, emote);
            });
        }

        public async Task UseConsumableAsync(Lot lot)
        {
            foreach (var task in await FindActiveTasksAsync<UseConsumableTask>())
            {
                await task.Progress(lot);
            }

            await SearchForNewAchievementsAsync<UseConsumableTask>(MissionTaskType.UseConsumable, lot, async task =>
            {
                await task.Progress(lot);
            });
        }

        public async Task UseSkillAsync(int skillId)
        {
            foreach (var task in await FindActiveTasksAsync<UseSkillTask>())
            {
                await task.Progress(skillId);
            }

            await SearchForNewAchievementsAsync<UseSkillTask>(MissionTaskType.UseSkill, skillId, async task =>
            {
                await task.Progress(skillId);
            });
        }

        public async Task ObtainItemAsync(Lot lot)
        {
            foreach (var task in await FindActiveTasksAsync<ObtainItemTask>())
            {
                await task.Progress(lot);
            }

            await SearchForNewAchievementsAsync<ObtainItemTask>(MissionTaskType.ObtainItem, lot, async task =>
            {
                await task.Progress(lot);
            });
        }

        public async Task MissionCompleteAsync(int id)
        {
            foreach (var task in await FindActiveTasksAsync<MissionCompleteTask>())
            {
                await task.Progress(id);
            }

            await SearchForNewAchievementsAsync<MissionCompleteTask>(MissionTaskType.MissionComplete, id, async task =>
            {
                await task.Progress(id);
            });
        }

        public async Task FlagAsync(int flag)
        {
            foreach (var task in await FindActiveTasksAsync<FlagTask>())
            {
                await task.Progress(flag);
            }

            await SearchForNewAchievementsAsync<FlagTask>(MissionTaskType.Flag, flag, async task =>
            {
                await task.Progress(flag);
            });
        }
        
        // TODO: Improve
        private async Task SearchForNewAchievementsAsync<T>(MissionTaskType type, Lot lot, Func<T, Task> progress = null) where T : MissionTaskBase
        { 
            await using var cdClient = new CdClientContext();
            
            //
            // Collect tasks which fits the requirements of this action.
            //

            var otherTasks = new List<MissionTasks>();

            if (TasksCache == null)
            {
                var tasks = await cdClient.MissionTasksTable.ToArrayAsync();

                var missions = await cdClient.MissionsTable.ToArrayAsync();

                var cache = new List<MissionTasks>();
                
                var missionCache = new List<Missions>();
                
                foreach (var task in tasks)
                {
                    var mission = missions.FirstOrDefault(m => m.Id == task.Id);
                    
                    if (mission == default) continue;

                    if (mission.OfferobjectID != -1 ||
                        mission.TargetobjectID != -1 ||
                        (mission.IsMission ?? true) ||
                        task.TaskType != (int) type)
                        continue;

                    cache.Add(task);
                    
                    if (missionCache.Contains(mission)) continue;

                    missionCache.Add(mission);
                }

                TasksCache = cache.ToArray();

                MissionCache = missionCache.ToArray();
            }
            
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var missionTask in TasksCache)
                if (MissionParser.GetTargets(missionTask).Contains(lot))
                    otherTasks.Add(missionTask);

            foreach (var task in otherTasks)
            {
                var mission = MissionCache.FirstOrDefault(m => m.Id == task.Id);

                if (mission == default) continue;

                //
                // Get the mission on the character. If present.
                //

                var characterMission = MissionInstances.FirstOrDefault(m => m.MissionId == mission.Id);

                //
                // Check if the player could passably start this achievement.
                //

                if (characterMission != default) continue;

                //
                // Check if player has the Prerequisites to start this achievement.
                //

                var hasPrerequisites = MissionParser.CheckPrerequiredMissions(
                    mission.PrereqMissionID,
                    GetCompletedMissions()
                );

                if (!hasPrerequisites) continue;

                //
                // Player can start achievement.
                //

                // Get Mission Id of new achievement.
                if (mission.Id == default) continue;
                
                var missionId = mission.Id.Value;

                //
                // Setup new achievement.
                //
                
                var instance = new MissionInstance(GameObject as Player, missionId);

                MissionInstances.Add(instance);

                await instance.LoadAsync();

                // TODO: Silent?
                await instance.StartAsync();

                var activeTask = instance.Tasks.First(t => t.TaskId == task.Uid);

                if (progress != null)
                {
                    var _ = Task.Run(async () => await progress(activeTask as T));
                }
            }
        }
    }
}