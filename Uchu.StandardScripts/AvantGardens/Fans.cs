using System;
using System.Numerics;
using System.Threading.Tasks;
using Uchu.Core;
using Uchu.World;
using Uchu.World.Scripting;

namespace Uchu.StandardScripts.AvantGardens
{
    [ZoneSpecific(ZoneId.AvantGardens)]
    public class Fans : Script
    {
        public override Task LoadAsync()
        {
            var ids = new[]
            {
                (19, 1),
                (19, 2),
                (20, 1)
            };

            foreach (var trigger in GetTriggers(ids))
            {
                var gameObject = trigger.GameObject;
                
                if (!gameObject.TryGetComponent<SwitchComponent>(out var switchComponent)) continue;

                var fanId = trigger.Trigger.Events[0].Commands[0].TargetName;

                GameObject fanObject = default;

                foreach (var zoneGameObject in Zone.GameObjects)
                {
                    //if (!zoneGameObject.TryGetComponent<LuaScriptComponent>(out _)) continue;
                    if (!zoneGameObject.Settings.TryGetValue("groupID", out var groupObj)) continue;

                    var groupId = (string) groupObj;

                    if (groupId != $"{fanId};") continue;
                    
                    fanObject = zoneGameObject;
                    
                    break;
                }
                
                switchComponent.OnActivated.AddListener(player =>
                {
                    ActivateFx(fanObject);
                    
                    return Task.CompletedTask;
                });
                
                switchComponent.OnDeactivated.AddListener(() =>
                {
                    DeactivateFx(fanObject);
                    
                    return Task.CompletedTask;
                });
                
                DeactivateFx(fanObject);
                
                gameObject.OnTick.AddListener(() =>
                {
                    if (switchComponent.State) return;
                    
                    foreach (var player in Zone.Players)
                    {
                        if (player?.Transform == default) return;
                        
                        if (!(Vector3.Distance(player.Transform.Position, gameObject.Transform.Position) < 2)) continue;

                        switchComponent.Activate(player);

                        Serialize(gameObject);
                    }
                });
            }
            
            return Task.CompletedTask;
        }

        private void ActivateFx(GameObject gameObject)
        {
            Console.WriteLine("Active");
            
            var group = (gameObject.Settings["groupID"] as string)?.Split(';')[0];
            
            if (group == default) return;

            gameObject.Animate("fan-off", true);

            gameObject.StopFX("fanOn");

            foreach (var fanObject in GetGroup(group))
            {
                // Nothing is found here
                if (!fanObject.TryGetComponent<PhantomPhysicsComponent>(out var physicsComponent)) continue;
                
                Serialize(fanObject);
                    
                physicsComponent.IsEffectActive = false;
            }

            GetGroup($"{group}fx")[0].Animate("trigger", true);
        }

        private void DeactivateFx(GameObject gameObject)
        {
            Console.WriteLine("Deactivated");
            
            var group = (gameObject.Settings["groupID"] as string)?.Split(';')[0];
            
            if (group == default) return;

            gameObject.Animate("fan-on", true);

            gameObject.PlayFX("fanOn", "fanOn", 495);

            foreach (var fanObject in GetGroup(group))
            {
                // Nothing is found here
                if (!fanObject.TryGetComponent<PhantomPhysicsComponent>(out var physicsComponent)) continue;
                
                physicsComponent.IsEffectActive = true;

                Serialize(fanObject);
            }

            GetGroup($"{group}fx")[0].Animate("idle", true);
        }
    }
}