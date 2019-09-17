using System.Collections.Generic;
using Uchu.Core;
using Uchu.World.Collections;
using Uchu.World.Parsers;

namespace Uchu.World
{
    [ServerComponent(Id = ReplicaComponentsId.Spawner)]
    public class SpawnerComponent : Component
    {
        public readonly List<GameObject> ActiveSpawns = new List<GameObject>();

        public SpawnerComponent()
        {
            OnStart += () => { GameObject.Layer = Layer.Spawner; };
        }

        public Lot SpawnTemplate { get; set; }

        public uint SpawnNodeId { get; set; }

        public LegoDataDictionary Settings { get; set; }

        public GameObject GetSpawnObject()
        {
            return GameObject.Instantiate(new LevelObject
            {
                Lot = SpawnTemplate,
                ObjectId = (ulong) Utils.GenerateObjectId(),
                Position = Transform.Position,
                Rotation = Transform.Rotation,
                Scale = 1,
                Settings = Settings
            }, Zone, this);
        }

        public GameObject Spawn()
        {
            var obj = GetSpawnObject();

            Start(obj);

            ActiveSpawns.Add(obj);

            obj.OnDestroyed += () => { ActiveSpawns.Remove(obj); };

            return obj;
        }
    }
}