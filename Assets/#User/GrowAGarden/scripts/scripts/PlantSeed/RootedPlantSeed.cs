using UnityEngine;


namespace GrowAGarden
{

    public class RootedPlantSeed : PlantSeed
    {

        [SerializeField] private MeshRenderer _PlantModel;
        [SerializeField] private Collider _PlantCollider;

        protected override void UpdateVisuals(bool IsSeed)
        {
            if (IsSeed)
            {
                Logger.Info($"SetState(true) '{gameObject.name}' — showing seed model");
                _PlantModel.enabled = false;
                _SeedModel.enabled = true;
                _PlantCollider.enabled = false;
                _SeedCollider.enabled = true;
            }
            else
            {
                Logger.Info($"SetState(false) '{gameObject.name}' — showing plant model");
                _PlantModel.enabled = true;
                _SeedModel.enabled = false;
                _PlantCollider.enabled = true;
                _SeedCollider.enabled = false;
            }
        }
    }
}
