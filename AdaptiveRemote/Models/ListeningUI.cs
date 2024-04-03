using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdaptiveRemote.Models;

internal class ListeningUI : RemoteLayoutElement
{
    public ListeningUI(string group)
        : base(group, nameof(ListeningUI).ToUpperInvariant())
    {
    }
}
