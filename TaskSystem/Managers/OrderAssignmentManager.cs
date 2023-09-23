using System;
using Packages.com.ianritter.unityscriptingtools.Runtime.Services.CustomLogger;
using SpaceSim.Scripts.Runtime.AI.States;
using SpaceSim.Scripts.Runtime.AI.States.WorkerHandling;
using SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels;
using SpaceSim.Scripts.Runtime.Entities.Wares;
using UnityEngine;
using static Packages.com.ianritter.unityscriptingtools.Runtime.Services.TextFormatting.TextFormat;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.Managers
{
    public class OrderAssignmentManager : MonoBehaviour
    {
        [SerializeField] private float idleWorkerScanDelay = 3f;
        [SerializeField] private int scansBeforeCancel = 5;
        [SerializeField] private bool limitSingleResourceScanning;
        [SerializeField] private CustomLogger logger;

        private StateMachine _workerManagerStateMachine;

        private WareChannel _wareChannel;
        private WorkerManager _workerManager;
        private ResourceOrderManager _resourceOrderManager;

        private OrderAssignmentChannel _orderAssignmentChannel;
        private OrderManagerChannel _resourceOrderManagerChannel;

        private int _adjustedScansBeforeCancel;

        private int _currentOrderIndex;
        private Order _currentOrder;

        private void Awake()
        {
            _wareChannel = GetComponentInParent<WareChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_wareChannel)}", _wareChannel );
            _wareChannel.OnProductWareSet += SetScansBeforeCancel;
            
            _workerManager = GetComponent<WorkerManager>();
            logger.LogObjectAssignmentResult( $"{nameof(_workerManager)}", _workerManager );
            
            _resourceOrderManager = GetComponent<ResourceOrderManager>();
            logger.LogObjectAssignmentResult( $"{nameof(_resourceOrderManager)}", _resourceOrderManager );
            
            _orderAssignmentChannel = GetComponent<OrderAssignmentChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_orderAssignmentChannel)}", _orderAssignmentChannel );

            _resourceOrderManagerChannel = GetComponent<OrderManagerChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_resourceOrderManagerChannel)}", _resourceOrderManagerChannel );

            InitializeStateMachine();
        }

        private void Update()
        {
            _workerManagerStateMachine.Tick();
        }

        private void SetScansBeforeCancel( Ware productWare )
        {
            logger.LogStart();

            _adjustedScansBeforeCancel = limitSingleResourceScanning
                ? scansBeforeCancel
                : ( productWare.GetNumberOfResources() == 1 )
                    ? 0
                    : scansBeforeCancel;

            logger.LogEnd();
        }

        public int GetScansBeforeCancel() => _adjustedScansBeforeCancel;
        
        private void InitializeStateMachine()
        {
            // State Machine
            _workerManagerStateMachine = new StateMachine();

            // States
            var idle = new IdleManager( logger );
            var assignOrders = new AssignOrders( this, _workerManager, idleWorkerScanDelay, logger, _orderAssignmentChannel );

            idle.OnIdleStarted += OnIdleStarted;
            
            // Transitions
            void AddTrans( IState from, IState to, Func<bool> condition ) => _workerManagerStateMachine.AddTransition( from, to, condition );

            AddTrans( idle, assignOrders, _resourceOrderManager.HasOpenOrder );
            AddTrans( assignOrders, idle, _resourceOrderManager.HasNoOpenOrders );
            
            // Default state
            _workerManagerStateMachine.SetState( idle );
        }

        private void OnIdleStarted()
        {
            logger.Log( "No more open orders are available. Switching to idle." );
            _orderAssignmentChannel.OnOrderAssignmentManagerStatusUpdatedNotify( "All Workers are Busy" );
        }

        public Order GetNextOrder()
        {
            logger.LogStart();
            // An open order is known to be available, but we don't know which one.
            // Start with the current index, then cycle through the rest of the orders until the open order is found.
            // If all orders are open, this will result in scanning one per scan, scanning them all in order before looping back to the start.
            for (int i = 0; i < _resourceOrderManager.GetOrderCount(); i++)
            {
                _currentOrder = _resourceOrderManager.GetOrderByIndex( _currentOrderIndex );
                logger.Log( $"Checking order at index {GetColoredStringYellow( i.ToString() )}" );
                if ( _currentOrder.IsOpen() )
                {
                    logger.LogEnd( $"Returning open order {_currentOrder.GetOrderName()}." );
                    return _currentOrder;
                }
                logger.Log( $"Order at index {GetColoredStringYellow( i.ToString() )} is not assigned. Advancing to next index." );
                AdvanceToNextOrderIndex();
            }

            logger.LogEnd( "No open orders were found!" );
            return null;
        }

        public void AdvanceToNextOrderIndex() => _currentOrderIndex = ( _currentOrderIndex + 1 ) % ( _resourceOrderManager.GetOrderCount() );

        public int GetCurrentOrderIndex() => _currentOrderIndex;
    }
}