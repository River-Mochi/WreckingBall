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

        // Explicit “no IconFlags” value (matches enum default = 0).
        private const IconFlags kNoIconFlags = (IconFlags)0;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_AbandonQueue = new NativeQueue<Entity>(Allocator.Persistent);
            m_DestroyQueue = new NativeQueue<Entity>(Allocator.Persistent);

            // Vanilla systems used for simulation timing and icon handling.
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

            // Global building configuration (de-facto singleton).
            m_BuildingConfigQuery = GetEntityQuery(
                ComponentType.ReadOnly<BuildingConfigurationData>());

            // Event archetypes used by DestroyAbandonedSystem.
            m_DamageEventArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<Damage>());

            m_DestroyEventArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<Destroy>());

#if DEBUG
            Mod.Log.Info("[WreckingBallSystem] Created.");
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
            EntityManager em = EntityManager;

            // Shared icon state for this frame (used by both Abandon and Destroy).
            BuildingConfigurationData buildingConfig = default;
            IconCommandBuffer iconBuffer = default;
            bool haveConfig = false;
            bool haveIconBuffer = false;
            IconCommandSystem? iconSystem = m_IconCommandSystem;

            // --- ABANDON: realistic abandoned state for a single building ---

            while (m_AbandonQueue.TryDequeue(out Entity entity))
            {
#if DEBUG
                Mod.Log.Info($"[WreckingBallSystem] Processing ABANDON for {entity}.");
#endif
                if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                {
                    continue;
                }

                Building building = em.GetComponentData<Building>(entity);

                // Clear conflicting flags.
                if (em.HasComponent<Destroyed>(entity))
                {
                    em.RemoveComponent<Destroyed>(entity);
                }

                if (em.HasComponent<Condemned>(entity))
                {
                    em.RemoveComponent<Condemned>(entity);
                }

                // Ensure the Abandoned tag is present, but DO NOT touch
                // m_AbandonmentTime if it already exists. Vanilla systems
                // own that timer (same behaviour as Eminent Domain).
                if (!em.HasComponent<Abandoned>(entity))
                {
                    em.AddComponent<Abandoned>(entity);
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

                // Utilities: abandoned buildings stop consuming/producing.
                if (em.HasComponent<ElectricityConsumer>(entity))
                {
                    em.RemoveComponent<ElectricityConsumer>(entity);  }

                if (em.HasComponent<WaterConsumer>(entity))
                {
                    em.RemoveComponent<WaterConsumer>(entity);  }

                if (em.HasComponent<GarbageProducer>(entity))
                {
                    em.RemoveComponent<GarbageProducer>(entity);  }

                if (em.HasComponent<MailProducer>(entity))
                {
                    em.RemoveComponent<MailProducer>(entity);  }

                // Crime: make abandoned buildings a bit worse for crime,
                // mirroring PTG / vanilla level-down behaviour.
                if (em.HasComponent<CrimeProducer>(entity))
                {
                    CrimeProducer crimeProducer = em.GetComponentData<CrimeProducer>(entity);
                    crimeProducer.m_Crime *= 2f;
                    em.SetComponentData(entity, crimeProducer);
                }

                // Renters: clear renters and their PropertyRenter components.
                if (em.HasBuffer<Renter>(entity))
                {
                    DynamicBuffer<Renter> renters = em.GetBuffer<Renter>(entity);
                    for (int i = renters.Length - 1; i >= 0; i--)
                    {
                        Entity renterEntity = renters[i].m_Renter;
                        if (em.Exists(renterEntity) && em.HasComponent<PropertyRenter>(renterEntity))
                        {
                            em.RemoveComponent<PropertyRenter>(renterEntity);
                        }

                        renters.RemoveAt(i);
                    }
                }

                // Icons: remove any previous generic problem icons and
                // add the specific Abandoned notification when possible.
                if (haveIconBuffer)
                {
                    iconBuffer.Remove(entity, IconPriority.Problem);
                    iconBuffer.Remove(entity, IconPriority.FatalProblem);
                    iconBuffer.Add(
                        entity,
                        buildingConfig.m_AbandonedNotification,
                        IconPriority.FatalProblem,
                        IconClusterLayer.Default,
                        kNoIconFlags,
                        Entity.Null,
                        false,
                        false,
                        false,
                        0f);
                }

                // Mark building as updated this frame.
                if (!em.HasComponent<Updated>(entity))
                {
                    em.AddComponent<Updated>(entity);    }

                // Nudge the road edge as well so utilities / networks refresh.
                if (building.m_RoadEdge != Entity.Null
                    && em.Exists(building.m_RoadEdge)
                    && !em.HasComponent<Updated>(building.m_RoadEdge))
                {
                    em.AddComponent<Updated>(building.m_RoadEdge);
                }
            }

            // --- DESTROY: instant collapse using vanilla damage/destroy events ---
            // --- DESTROY: instant collapse using vanilla damage/destroy events ---

            while (m_DestroyQueue.TryDequeue(out Entity entity))
            {
#if DEBUG
                Mod.Log.Info($"[WreckingBallSystem] Processing DESTROY for {entity}.");
#endif
                if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                {
                    continue;
                }

                // Read building once; we use it later for the road-edge nudge.
                Building building = em.GetComponentData<Building>(entity);

                // Clear conflicting flags.
                if (em.HasComponent<Abandoned>(entity))
                {
                    em.RemoveComponent<Abandoned>(entity);
                }

                if (em.HasComponent<Condemned>(entity))
                {
                    em.RemoveComponent<Condemned>(entity);
                }

                // Lazily grab config + icon buffer once (if available). only set when needed.
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
                //  1) Damage event.
                Entity damageEvent = em.CreateEntity(m_DamageEventArchetype);
                em.SetComponentData(damageEvent, new Damage(entity, new float3(1f, 0f, 0f)));

                //  2) Destroy event.
                Entity destroyEvent = em.CreateEntity(m_DestroyEventArchetype);
                em.SetComponentData(destroyEvent, new Destroy(entity, Entity.Null));

                // Mirror DestroyAbandonedSystem icon behaviour when possible:
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
                        kNoIconFlags,
                        Entity.Null,
                        false,
                        false,
                        false,
                        0f);
                }

                // Mark building as updated this frame, alerts game (e.g., DestroySystem or AbandonSystem)
                // that state has changed so they can react.
                if (!em.HasComponent<Updated>(entity))      // does it already have Updated?
                {       em.AddComponent<Updated>(entity);   // if not, add it
                }
                // Nudge road edge so networks refresh after collapse too.
                if (building.m_RoadEdge != Entity.Null
                    && em.Exists(building.m_RoadEdge)
                    && !em.HasComponent<Updated>(building.m_RoadEdge))
                {  em.AddComponent<Updated>(building.m_RoadEdge);
                }
            }
        }
    }
}
