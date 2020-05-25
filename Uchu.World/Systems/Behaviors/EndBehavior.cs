using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class EndBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.End;
        
        public BehaviorBase StartAction { get; set; }
        
        public int UseTarget { get; set; }
        
        public override async Task BuildAsync()
        {
            StartAction = await GetBehavior("action");

            UseTarget = await GetParameter<int>("use_target");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
        }
    }
}