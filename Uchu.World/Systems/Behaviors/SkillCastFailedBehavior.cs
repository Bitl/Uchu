using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class SkillCastFailedBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.SkillCastFailed;
        
        public int EffectId { get; set; }
        
        public override async Task BuildAsync()
        {
            EffectId = await GetParameter<int>("effect_id");
        }

        public override Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            return Task.CompletedTask;
        }
    }
}