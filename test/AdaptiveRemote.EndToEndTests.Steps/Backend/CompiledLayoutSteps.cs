using System.Text.Json.Serialization.Metadata;
using AdaptiveRemote.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Backend;

[Binding]
public class CompiledLayoutSteps : StepsBase
{
    [StepArgumentTransformation(nameof(CompiledLayout))]
    public static JsonTypeInfo CompiledLayoutJsonTypeInfo() => LayoutContractsJsonContext.Default.CompiledLayout;

    [StepArgumentTransformation("(TiVo|IR|Lifecycle)")]
    public static CommandType StringToCommandType(string commandType)
        => Enum.Parse<CommandType>(commandType);

    [Then(@"the CompiledLayout in the response body has a(n) {CommandType} command named {string}")]
    public void ThenTheCompiledLayoutInTheResponseBodyHasACommandOfTypeWithName(CommandType expectedType, string expectedName)
    {
        CompiledLayout? layout = TestClient.LastResponseObject as CompiledLayout;
        Assert.IsNotNull(layout, "Last response was not parsed as a CompiledLayout");

        IEnumerable<CommandDefinitionDto> commands = EnumerateAllCommands(layout.Elements);

        Assert.IsTrue(commands.Any(c => c.Type == expectedType && c.Name == expectedName),
            $"Expected to find a command of type {expectedType} with name '{expectedName}' in the CompiledLayout, but it was not found. Commands found: {string.Join(", ", commands.Select(c => $"{c.Type}:{c.Name}"))}");
    }

    private static IEnumerable<CommandDefinitionDto> EnumerateAllCommands(IEnumerable<LayoutElementDto> elements)
    {
        Stack<IEnumerator<LayoutElementDto>> stack = new();
        stack.Push(elements.GetEnumerator());

        while (stack.Count > 0)
        {
            IEnumerator<LayoutElementDto> enumerator = stack.Pop();
            while (enumerator.MoveNext())
            {
                LayoutElementDto current = enumerator.Current;
                if (current is CommandDefinitionDto command)
                {
                    yield return command;
                }
                else if (current is LayoutGroupDefinitionDto container)
                {
                    stack.Push(enumerator);
                    enumerator = container.Children.GetEnumerator();
                }
            }
        }
    }
}
