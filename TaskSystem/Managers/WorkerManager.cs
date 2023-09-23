using System.Collections.Generic;
using System.Linq;
using Packages.com.ianritter.unityscriptingtools.Runtime.Services.CustomLogger;
using SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels;
using SpaceSim.Scripts.Runtime.Entities.Factories.Docking;
using SpaceSim.Scripts.Runtime.Entities.Workers;
using UnityEngine;
using static Packages.com.ianritter.unityscriptingtools.Runtime.Services.TextFormatting.TextFormat;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.Managers
{
    public class WorkerManager : MonoBehaviour
    {
        [SerializeField] private CustomLogger logger;
        
        private readonly List<Worker> _workers = new List<Worker>();
        private WorkerManagerChannel _workerManagerChannel;
        private DockingModule _dockingModule;
        private int _maxWorkerCount;

        
        private void Awake()
        {
            _workerManagerChannel = GetComponentInParent<WorkerManagerChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_workerManagerChannel)}", _workerManagerChannel );
            
            _dockingModule = GetComponentInChildren<DockingModule>();
            logger.LogObjectAssignmentResult( $"{nameof(_dockingModule)}", _dockingModule );
            _maxWorkerCount = _dockingModule.GetDockingBayCount();
        }
        
        /// <summary>
        ///     Worker is initialized and added to the list of available workers.
        /// </summary>
        public void AddWorker( Worker worker )
        {
            if ( _workers.Count >= _maxWorkerCount )
            {
                Debug.LogWarning( $"Factory {GetColoredStringYellow(name)} already has the maximum number of workers ({GetColoredStringGreen( _maxWorkerCount.ToString() )})." );
                return;
            }
            
            _workers.Add( worker );
            worker.InitializeWorker( this );
            worker.GetWorkerChannel().OnOrderAssigned += OnOrderAssigned;
            
            _workerManagerChannel.OnIdleShipCountUpdatedNotify( GetIdleShipCount() );
        }

        /// <summary>
        ///     Returns a worker that currently has no task assigned.
        /// </summary>
        public Worker GetIdleWorker() => _workers.FirstOrDefault( ship => ship.IsIdle() );
        
        /// <summary>
        ///     Returns true if the Worker Manager's max worker count has not yet been exceeded.
        /// </summary>
        public bool HasRoomForAnotherWorker() => _workers.Count < _maxWorkerCount;

        /// <summary>
        ///     Returns the number of workers present in the Worker Manager's list, including both idle and currently occupied.
        /// </summary>
        public int GetTotalWorkerCount() => _workers.Count;
        
        private void OnOrderAssigned( Order order ) => _workerManagerChannel.OnIdleShipCountUpdatedNotify( GetIdleShipCount() );

        private int GetIdleShipCount() => _workers.Count( worker => worker.IsIdle() );

    }
}
