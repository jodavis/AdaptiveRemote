using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

internal static class IRemoteDefinitionServiceExtensions
{
    internal static IEnumerable<Command> GetCommands(this IRemoteDefinitionService service)
    {
        return service.GetAllElements().OfType<Command>();
    }

    internal static IEnumerable<CommandType> GetCommands<CommandType>(this IRemoteDefinitionService service)
        where CommandType : Command
    {
        return service.GetAllElements().OfType<CommandType>();
    }

    internal static ElementType GetElement<ElementType>(this IRemoteDefinitionService service)
    {
        return service.GetAllElements().OfType<ElementType>().First();
    }

    private static IEnumerable<RemoteLayoutElement> GetAllElements(this IRemoteDefinitionService service)
    {
        Stack<IEnumerator<RemoteLayoutElement>> stack = new();
        IEnumerator<RemoteLayoutElement> currentIter = new[] { service.RemoteRoot }.AsEnumerable().GetEnumerator();

        while (true)
        {
            if (currentIter.MoveNext())
            {
                yield return currentIter.Current;

                if (currentIter.Current is LayoutGroup group)
                {
                    stack.Push(currentIter);
                    currentIter = group.Elements.GetEnumerator();
                }
            }
            else if (stack.Count > 0)
            {
                currentIter = stack.Pop();
            }
            else
            {
                break;
            }
        }
    }
}
