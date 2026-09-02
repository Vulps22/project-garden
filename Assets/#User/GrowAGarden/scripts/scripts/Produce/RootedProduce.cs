using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Produce that took its plant's place in the ground, rather than hanging off one: a carrot, a
    /// turnip.
    ///
    /// Its distinctness is not the ripen time — it is that **it owns the plot**. The rooted plant
    /// despawns the moment it is grown, so if nothing held the ground a player could plant a seed
    /// into a plot that visibly still contains a carrot. Which is also true to life: a carrot in
    /// the earth occupies that earth until somebody pulls it.
    ///
    /// It arrives at full size. All of its growing happened as a plant, so scaling it up again
    /// would replay the same growth twice.
    /// </summary>
    public class RootedProduce : Produce
    {
        protected override void ApplyScale()
        {
            if (_bodyToScale == null || IsHarvested) return;
            _bodyToScale.localScale = Vector3.one * _maxScale;
        }
    }
}
