// Systems/WreckingBallSection.cs
// Purpose: Selected Info Panel section that exposes "Abandon" and "Destroy"
//          actions for the currently selected building via UI triggers.

namespace WreckingBall
{
    using Colossal.Logging;
    using Colossal.UI.Binding;
    using Game.Buildings;
    using Game.Common;
    using Game.Tools;
    using Game.UI.InGame;
    using Unity.Entities;
    using UnityEngine.Scripting;

    public sealed partial class WreckingBallSection : InfoSectionBase
    {
        private ILog? m_Log;
        private WreckingBallSystem? m_System;

        // Logical group name inside the SIP layout
        protected override string group => "WreckingBallSection";

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();

            m_Log = Mod.Log;

            // Simulation system that actually performs the work
            m_System = World.DefaultGameObjectInjectionWorld?
                .GetOrCreateSystemManaged<WreckingBallSystem>();

            // Wire UI triggers to our local handlers
            AddBinding(new TriggerBinding(Mod.ModId, "AbandonBuilding", AbandonBuilding));
            AddBinding(new TriggerBinding(Mod.ModId, "DestroyBuilding", DestroyBuilding));

#if DEBUG
            m_Log?.Info("[WreckingBallSection] OnCreate completed.");
#endif
        }

        [Preserve]
        protected override void OnUpdate()
        {
            base.OnUpdate();
            visible = IsVisible();
        }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            // No additional properties needed; React side just shows two buttons.
        }

        protected override void OnProcess()
        {
            // No per-frame property updates required.
        }

        protected override void Reset()
        {
            // Nothing to reset here.
        }

        private bool IsVisible()
        {
            EntityManager em = EntityManager;
            Entity entity = selectedEntity;

            if (!em.Exists(entity))
            {
                return false;
            }

            if (!em.HasComponent<Building>(entity))
            {
                return false;
            }

            // Hide for deleted / temp / placeholder entities
            if (em.HasComponent<Deleted>(entity) || em.HasComponent<Temp>(entity))
            {
                return false;
            }

            // Only for buildings that actually have renters (matches EminentDomain behavior)
            return em.HasBuffer<Renter>(entity);
        }

        private void AbandonBuilding()
        {
#if DEBUG
            m_Log?.Info($"[WreckingBallSection] AbandonBuilding trigger received for {selectedEntity}.");
#endif

            Entity entity = selectedEntity;

            if (entity == Entity.Null)
            {
                return;
            }

            if (m_System != null)
            {
                m_System.RequestAbandon(entity);
            }
            else
            {
                // Fallback: apply directly if system is missing for some reason.
                EntityManager em = EntityManager;
                if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                {
                    return;
                }

                if (em.HasComponent<Destroyed>(entity))
                {
                    em.RemoveComponent<Destroyed>(entity);
                }

                if (em.HasComponent<Condemned>(entity))
                {
                    em.RemoveComponent<Condemned>(entity);
                }

                if (!em.HasComponent<Abandoned>(entity))
                {
                    em.AddComponent<Abandoned>(entity);
                }

                if (!em.HasComponent<Updated>(entity))
                {
                    em.AddComponent<Updated>(entity);
                }
            }

#if DEBUG
            m_Log?.Info($"[WreckingBallSection] AbandonBuilding requested for {selectedEntity}.");
#endif
        }

        private void DestroyBuilding()
        {
#if DEBUG
            m_Log?.Info($"[WreckingBallSection] DestroyBuilding trigger received for {selectedEntity}.");
#endif
            Entity entity = selectedEntity;

            if (entity == Entity.Null)
            {
                return;
            }

            if (m_System != null)
            {
                m_System.RequestDestroy(entity);
            }
            else
            {
                EntityManager em = EntityManager;
                if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                {
                    return;
                }

                if (em.HasComponent<Abandoned>(entity))
                {
                    em.RemoveComponent<Abandoned>(entity);
                }

                if (em.HasComponent<Condemned>(entity))
                {
                    em.RemoveComponent<Condemned>(entity);
                }

                if (!em.HasComponent<Destroyed>(entity))
                {
                    em.AddComponent<Destroyed>(entity);
                }

                if (!em.HasComponent<Updated>(entity))
                {
                    em.AddComponent<Updated>(entity);
                }
            }

#if DEBUG
            m_Log?.Info($"[WreckingBallSection] DestroyBuilding requested for {selectedEntity}.");
#endif
        }
    }
}
