using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// One PlantSlot's runtime state, held by PlotStateManager. Mirrors BearingPlant.ProduceSlot
    /// exactly, for the same reason: not [Serializable], not a MonoBehaviour, built in code at
    /// Awake from a plain Transform array — arrays of custom [Serializable] classes arrive empty
    /// from Somnium's asset-bundle export.
    /// </summary>
    internal class PlantSlotState
    {
        public readonly Transform Anchor;
        public bool IsOccupied;
        public IPlotOccupant Occupant;

        public PlantSlotState(Transform anchor) => Anchor = anchor;
    }
}
