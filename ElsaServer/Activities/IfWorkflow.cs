using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

namespace ElsaServer.Activities;

public class IfWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name= "Workflow IF";

        builder.Root = new If
        {
            Id = "If1",
            //Condition = new(context => DateTime.Now.IsDaylightSavingTime()),
            Condition = new Input<bool>(new Elsa.Expressions.Models.Expression("CSharp", "DateTime.Now.IsDaylightSavingTime()")),
            Then = new WriteLine("Hello to the light side!") { Id = "WriteLine1" },
            Else = new WriteLine("Hello to the dark side!") { Id = "WriteLine2" }
        };
    }
}
