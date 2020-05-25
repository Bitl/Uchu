using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class HealBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Heal;
        
        public int Health { get; set; }
        
        public override async Task BuildAsync()
        {
            Health = await GetParameter<int>("health");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            if (!branch.Target.TryGetComponent<Stats>(out var stats)) return;

            stats.Health = (uint) ((int) stats.Health + Health);
        }
    }
}