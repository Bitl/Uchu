using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchu.Core.Client;

namespace Uchu.World
{
    public struct Lot
    {
        public readonly int Id;

        public Lot(int id)
        {
            Id = id;
        }

        public static implicit operator int(Lot lot)
        {
            return lot.Id;
        }

        public static implicit operator Lot(int id)
        {
            return new Lot(id);
        }

        public override bool Equals(object obj)
        {
            return obj switch
            {
                int i => (i == Id),
                Lot l => (l.Id == Id),
                _ => false
            };
        }

        public bool Equals(Lot other)
        {
            return Id == other.Id;
        }

        public override string ToString()
        {
            return Id.ToString();
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public async Task<int> GetComponentIdAsync(ComponentId type)
        {
            var id = Id;
            
            await using var cdClient = new CdClientContext();
            
            var itemRegistryEntry = await cdClient.ComponentsRegistryTable.FirstOrDefaultAsync(
                r => r.Id == id && r.Componenttype == (int) type
            );

            return itemRegistryEntry?.Componentid ?? 0;
        }

        #region Consts

        public static readonly Lot ModularRocket = 6416;

        public static readonly Lot Spawner = 176;

        public const int Imagination = 935;
        public const int TwoImagination = 4035;
        public const int ThreeImagination = 11910;
        public const int FiveImagination = 11911;
        public const int TenImagination = 11918;
        public const int Health = 177;
        public const int TwoHealth = 11915;
        public const int ThreeHealth = 11916;
        public const int FiveHealth = 11917;
        public const int TenHealth = 11920;
        public const int Armor = 6431;
        public const int TwoArmor = 11912;
        public const int ThreeArmor = 11913;
        public const int FiveArmor = 11914;
        public const int TenArmor = 11919;

        public static readonly Lot RocketBuildNosecone = 6904;
        public static readonly Lot RocketBuildCockpit = 6910;
        public static readonly Lot RocketBuildTail = 6915;

        #endregion
    }
}