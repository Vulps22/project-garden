using System;

namespace GrowAGarden
{
    /// <summary>
    /// Announces that lifecycle state changed, so behaviours can re-evaluate themselves.
    ///
    /// Deliberately carries no payload: subscribers read what they need from the source rather
    /// than being handed a snapshot, so a new behaviour can be added without changing this
    /// interface or anything that raises it.
    /// </summary>
    public interface ILifecycleNotifier
    {
        event Action LifecycleChanged;
    }
}
