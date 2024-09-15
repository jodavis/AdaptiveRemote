using System.Text;
using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Conversation;

internal record ConversationResponse(IEnumerable<string> Phrases, IEnumerable<Command> Commands)
{
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(nameof(Phrases));
        builder.Append("=[");
        bool wroteOneItem = false;
        foreach (string phrase in Phrases)
        {
            if (wroteOneItem)
            {
                builder.Append(", ");
            }
            builder.AppendFormat("\"{0}\"", phrase);
            wroteOneItem = true;
        }
        builder.Append("], ");

        builder.Append(nameof(Commands));
        builder.Append("=[");
        wroteOneItem = false;
        foreach (object command in Commands)
        {
            if (wroteOneItem)
            {
                builder.Append(", ");
            }
            builder.AppendFormat("{0}", command);
            wroteOneItem = true;
        }
        builder.Append(']');

        return true;
    }

    public virtual bool Equals(ConversationResponse? other)
    {
        if (other is null)
        {
            return false;
        }

        using IEnumerator<string> thisPhrases = Phrases.GetEnumerator();
        using IEnumerator<string> otherPhrases = other.Phrases.GetEnumerator();

        while (thisPhrases.MoveNext())
        {
            if (!otherPhrases.MoveNext())
            {
                return false;
            }

            if (!string.Equals(thisPhrases.Current, otherPhrases.Current, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (otherPhrases.MoveNext())
        {
            return false;
        }

        using IEnumerator<Command> thisCommands = Commands.GetEnumerator();
        using IEnumerator<Command> otherCommands = other.Commands.GetEnumerator();

        while (thisCommands.MoveNext())
        {
            if (!otherCommands.MoveNext())
            {
                return false;
            }

            if (!ReferenceEquals(thisCommands.Current, otherCommands.Current))
            {
                return false;
            }
        }

        if (otherCommands.MoveNext())
        {
            return false;
        }

        return true;
    }

    public override int GetHashCode() => ToString().GetHashCode();
}
