namespace Vulps.GrowAGarden.Ownership
{
    /// <summary>
    /// Every message id sent on a Plot's single shared NetworkMessenger, across every manager that
    /// subscribes to it (PlotLeaseManager, PlotStateManager, anything added later).
    ///
    /// One list because the bridge has one MessageToAll event, not one per subscriber — two
    /// managers each independently starting their own enum at 0 collide silently, since the
    /// receiver never throws, it just misreads the payload as whatever its own enum says byte 0
    /// means. Append new values, never renumber or reuse — these are wire ids.
    /// </summary>
    internal enum PlotMessageType : byte
    {
        OwnerChanged = 0,          // PlotLeaseManager
        SlotOccupancyChanged = 1,  // PlotStateManager — incremental (index, occupied)
        PlantRequest = 2,          // PlotStateManager
        Planted = 3,               // PlotStateManager
        FullStateSync = 4,         // PlotStateManager — 24-bit occupancy mask, sent on join
        TeammatesChanged = 5,      // PlotLeaseManager — full resend, count byte + N ids
        TeammateRemoveRequest = 6, // PlotLeaseManager — client to master, one id
    }
}
