// Systems/WreckingBallSystem.cs
// Purpose: Simulation system to process Abandon / Destroy requests for buildings,
//          ensuring proper flags and "Updated" so vanilla systems can spawn
//          icons, rubble, etc.

namespace WreckingBall
{
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Unity.Collections;
    using Unity.Entities;

    public sealed partial class WreckingBallSystem : GameSystemBase
    {
        private NativeQueue<Entity> m_AbandonQueue;
        private NativeQueue<Entity> m_DestroyQueue;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_AbandonQueue = new NativeQueue<Entity>(Allocator.Persistent);
            m_DestroyQueue = new NativeQueue<Entity>(Allocator.Persistent);

#if DEBUG
            WreckingBall.Mod.Log.Info("[WreckingBallSystem] Created.");
#endif
        }

        protected override void OnDestroy()
        {
            if (m_AbandonQueue.IsCreated)
            {
                m_AbandonQueue.Dispose();
            }

            if (m_DestroyQueue.IsCreated)
            {
                m_DestroyQueue.Dispose();
            }

            base.OnDestroy();
        }

        public void RequestAbandon(Entity building)
        {
            if (!EntityManager.Exists(building))
            {
                return;
            }

            m_AbandonQueue.Enqueue(building);
        }

        public void RequestDestroy(Entity building)
        {
            if (!EntityManager.Exists(building))
            {
                return;
            }

            m_DestroyQueue.Enqueue(building);
        }

        protected override void OnUpdate()
        {
            // Process abandon requests
            while (m_AbandonQueue.TryDequeue(out var entity))
            {
#if DEBUG
                WreckingBall.Mod.Log.Info($"[WreckingBallSystem] Processing ABANDON for {entity}.");
#endif

                if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<Building>(entity))
                {
                    continue;
                }

                // Clear conflicting flags
                if (EntityManager.HasComponent<Destroyed>(entity))
                {
                    EntityManager.RemoveComponent<Destroyed>(entity);
                }

                if (EntityManager.HasComponent<Condemned>(entity))
                {
                    EntityManager.RemoveComponent<Condemned>(entity);
                }

                // Mark as abandoned
                if (!EntityManager.HasComponent<Abandoned>(entity))
                {
                    EntityManager.AddComponent<Abandoned>(entity);
                }

                // Nudge simulation to recalc and spawn icons, etc.
                if (!EntityManager.HasComponent<Updated>(entity))
                {
                    EntityManager.AddComponent<Updated>(entity);
                }
            }

            // Process destroy requests
            while (m_DestroyQueue.TryDequeue(out var entity))
            {
#if DEBUG
                WreckingBall.Mod.Log.Info($"[WreckingBallSystem] Processing DESTROY for {entity}.");
#endif
                if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<Building>(entity))
                {
                    continue;
                }

                // Clear conflicting flags
                if (EntityManager.HasComponent<Abandoned>(entity))
                {
                    EntityManager.RemoveComponent<Abandoned>(entity);
                }

                if (EntityManager.HasComponent<Condemned>(entity))
                {
                    EntityManager.RemoveComponent<Condemned>(entity);
                }

                // Mark as destroyed (vanilla systems should handle rubble / cleanup)
                if (!EntityManager.HasComponent<Destroyed>(entity))
                {
                    EntityManager.AddComponent<Destroyed>(entity);
                }

                // Force refresh
                if (!EntityManager.HasComponent<Updated>(entity))
                {
                    EntityManager.AddComponent<Updated>(entity);
                }
            }
        }
    }
}
