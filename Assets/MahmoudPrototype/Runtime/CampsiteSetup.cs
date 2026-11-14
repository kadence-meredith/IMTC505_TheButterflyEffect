using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class CampsiteSetup : MonoBehaviour
    {
        public const string Version = "2026.11.14.1";
        public DesktopPlayer player;
        public CigaretteItem cigarette;
        public DisposalBin disposalBin;
        public FireTarget campfire;
        public FireTarget cigaretteFire;
        public LighterItem lighter;
        public MarshmallowItem marshmallow;
        public WaterBucketItem bucket;
        public FireworkStation fireworks;
        public Transform marshmallowTable;
        public Transform fireworksTable;
        public Transform playerSpawn;
        public GameObject[] authoredFireVisuals;

        [ContextMenu("Print Current Results")]
        public void PrintCurrentResults()
        {
            PrototypeSession session = PrototypeSession.Instance;
            if (session == null) return;
            int errors = session.state.UnsafeStations(EndingRule.CampsiteFinal);
            Debug.Log($"Safe choices: {3 - errors}/3 | Errors: {errors}/3 | " +
                $"Actions: {session.state.totalActions} | Marshmallows eaten: {session.state.marshmallowsEaten} | " +
                $"Fireworks launched: {session.state.fireworksLaunched}", this);
        }
    }
}
