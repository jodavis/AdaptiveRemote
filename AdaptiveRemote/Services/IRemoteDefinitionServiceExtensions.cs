using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services;

internal static class IRemoteDefinitionServiceExtensions
{
    internal static IEnumerable<Command> GetCommands(this IRemoteDefinitionService service, string remoteName)
    {
        return service.GetAllElements(remoteName).OfType<Command>();
    }

    internal static ElementType GetElement<ElementType>(this IRemoteDefinitionService service, string remoteName)
    {
        return service.GetAllElements(remoteName).OfType<ElementType>().First();
    }

    private static IEnumerable<RemoteLayoutElement> GetAllElements(this IRemoteDefinitionService service, string remoteName)
    {
        Stack<IEnumerator<RemoteLayoutElement>> stack = new();
        IEnumerator<RemoteLayoutElement> currentIter = new[] { service.GetRoot(remoteName) }.AsEnumerable().GetEnumerator();

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
