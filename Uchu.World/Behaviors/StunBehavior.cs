using System.Threading.Tasks;

namespace Uchu.World.Behaviors
{
    public class StunBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Stun;
        
        public int StunCaster { get; set; }
        
        public override async Task BuildAsync()
        {
            StunCaster = await GetParameter<int>("stun_caster");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            await base.ExecuteAsync(context, branchContext);
            
            if (StunCaster == 1) return;

            context.Reader.ReadBit();

            context.Writer.WriteBit(false);
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            if (StunCaster == 1) return;

            context.Writer.WriteBit(false);
        }
    }
}