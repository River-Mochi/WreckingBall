// Systems/WreckingBallSystem.cs
// Purpose: Simulation system to process Abandon / Destroy requests for buildings,
//          routing them through the vanilla abandon/damage/destroy pipeline so
//          icons, rubble, VFX and SFX all behave like the base game.

namespace WreckingBall
{
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public sealed partial class WreckingBallSystem : GameSystemBase
    {
        private NativeQueue<Entity> m_AbandonQueue;
        private NativeQueue<Entity> m_DestroyQueue;

        private SimulationSystem? m_SimulationSystem;
        private IconCommandSystem? m_IconCommandSystem;

        private EntityQuery m_BuildingConfigQuery;
        private EntityArchetype m_DamageEventArchetype;
        private EntityArchetype m_DestroyEventArchetype;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_AbandonQueue = new NativeQueue<Entity>(Allocator.Persistent);
            m_DestroyQueue = new NativeQueue<Entity>(Allocator.Persistent);

            // Vanilla systems we piggy-back on
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

            // Global building configuration (de-facto singleton)
            m_BuildingConfigQuery = GetEntityQuery(
                ComponentType.ReadOnly<BuildingConfigurationData>());

            // Event archetypes used by DestroyAbandonedSystem
            m_DamageEventArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<Damage>());

            m_DestroyEventArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<Destroy>());

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
            var em = EntityManager;

            // Use the current simulation frame for Abandoned timer.
            uint frame = 0;
            if (m_SimulationSystem != null)
            {
                frame = m_SimulationSystem.frameIndex;
            }

            // --- ABANDON: vanilla-style delayed collapse via DestroyAbandonedSystem ---

            while (m_AbandonQueue.TryDequeue(out var entity))
            {
#if DEBUG
                WreckingBall.Mod.Log.Info($"[WreckingBallSystem] Processing ABANDON for {entity}.");
#endif
                if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                {
                    continue;
                }

                // Clear conflicting flags
                if (em.HasComponent<Destroyed>(entity))
                {
                    em.RemoveComponent<Destroyed>(entity);
                }

                if (em.HasComponent<Condemned>(entity))
                {
                    em.RemoveComponent<Condemned>(entity);
                }

                // Mark as abandoned and reset the abandonment timer, so
                // DestroyAbandonedSystem will apply the configured delay.
                if (em.HasComponent<Abandoned>(entity))
                {
                    var abandoned = em.GetComponentData<Abandoned>(entity);
                    abandoned.m_AbandonmentTime = frame;
                    em.SetComponentData(entity, abandoned);
                }
                else
                {
                    em.AddComponentData(entity, new Abandoned
                    {
                        m_AbandonmentTime = frame,
                    });
                }

                // Nudge simulation to recalc icons/effects/etc.
                if (!em.HasComponent<Updated>(entity))
                {
                    em.AddComponent<Updated>(entity);
                }
            }

            // --- DESTROY: instant collapse using vanilla damage/destroy events ---

            BuildingConfigurationData buildingConfig = default;
            IconCommandBuffer iconBuffer = default;
            bool haveConfig = false;
            bool haveIconBuffer = false;
            var iconSystem = m_IconCommandSystem;

            while (m_DestroyQueue.TryDequeue(out var entity))
            {
#if DEBUG
                WreckingBall.Mod.Log.Info($"[WreckingBallSystem] Processing DESTROY for {entity}.");
#endif
                if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                {
                    continue;
                }

                // Clear conflicting flags
                if (em.HasComponent<Abandoned>(entity))
                {
                    em.RemoveComponent<Abandoned>(entity);
                }

                if (em.HasComponent<Condemned>(entity))
                {
                    em.RemoveComponent<Condemned>(entity);
                }

                // Lazily grab config + icon buffer once (if available).
                if (!haveConfig
                    && iconSystem != null
                    && !m_BuildingConfigQuery.IsEmptyIgnoreFilter)
                {
                    buildingConfig = m_BuildingConfigQuery.GetSingleton<BuildingConfigurationData>();
                    iconBuffer = iconSystem.CreateCommandBuffer();
                    haveConfig = true;
                    haveIconBuffer = true;
                }

                // Kick the same event-based collapse path vanilla uses:
                //  1) Damage event
                var damageEvent = em.CreateEntity(m_DamageEventArchetype);
                em.SetComponentData(damageEvent, new Damage(entity, new float3(1f, 0f, 0f)));

                //  2) Destroy event
                var destroyEvent = em.CreateEntity(m_DestroyEventArchetype);
                em.SetComponentData(destroyEvent, new Destroy(entity, Entity.Null));

                // Mirror DestroyAbandonedSystem's icon behaviour when possible:
                // remove existing problem icons and add the collapsed-building icon.
                if (haveIconBuffer)
                {
                    iconBuffer.Remove(entity, IconPriority.Problem);
                    iconBuffer.Remove(entity, IconPriority.FatalProblem);
                    iconBuffer.Add(
                        entity,
                        buildingConfig.m_AbandonedCollapsedNotification,
                        IconPriority.FatalProblem,
                        IconClusterLayer.Default,
                        (IconFlags)0,
                        Entity.Null,
                        false,
                        false,
                        false,
                        0f);
                }

                // Optional: mark building as having been updated this frame.
                if (!em.HasComponent<Updated>(entity))
                {
                    em.AddComponent<Updated>(entity);
                }
            }
        }
    }
}
