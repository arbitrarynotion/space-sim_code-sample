using System;
using Packages.com.ianritter.unityscriptingtools.Runtime.Services.CustomLogger;
using SpaceSim.Scripts.Runtime.AI.States;
using SpaceSim.Scripts.Runtime.AI.States.ResourceManagement;
using SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels;
using SpaceSim.Scripts.Runtime.Entities.Factories.Docking;
using SpaceSim.Scripts.Runtime.Entities.Wares;
using SpaceSim.Scripts.Runtime.Entities.Workers;
using SpaceSim.Scripts.Runtime.UI.Entities.Factories;
using UnityEngine;
using static Packages.com.ianritter.unityscriptingtools.Runtime.Services.TextFormatting.TextFormat;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.Managers
{
    public class ResourceOrderManager : MonoBehaviour
    {
        [SerializeField] private float stockScanDelay = 3f;
        
        [SerializeField] private CustomLogger logger;
        [SerializeField] private CustomLogger orderHandlerLogger;
        [SerializeField] private CustomLogger orderLogger;

        private int[] _resourceStockInbound;

        private OrderAssignmentManager _orderAssignmentManager;
        private DockingModule _dockingModule;
        private OrderHandler _resourceOrderHandler;
        
        private WareDepot _wareDepot;
        private WareChannel _wareChannel;
        private OrderManagerChannel _resourceOrderManagerChannel;
        private OrderPanelHandler _orderPanelHandler;
        private WareStorage _wareStorage;
        private WorkerManager _workerManager;
        
        private StateMachine _orderManagementStateMachine;

        private bool _freeOrderAvailable = false;


#region Getters
        
        public int GetOrderCount() => _resourceOrderHandler.GetOrderCount();
        public Order GetOrderByIndex( int index ) => _resourceOrderHandler.GetOrder( index );
        public bool HasOpenOrder() => _resourceOrderHandler.HasOpenOrder();
        public bool HasNoOpenOrders() => _resourceOrderHandler.HasOpenNoOrders();
        public Order GetOpenOrder() => _resourceOrderHandler.GetOpenOrder();

        public DockingModule GetDockingModule() => _dockingModule;
        public OrderManagerChannel GetOrderManagerChannel() => _resourceOrderManagerChannel;

        public bool HasRoomForAnotherWorker() => _workerManager.HasRoomForAnotherWorker();
        public bool OrderIsAvailable() => _resourceOrderHandler.OrderIsAvailable();
        
        public int GetResourceStockInbound( int wareIndex ) => _resourceStockInbound[wareIndex];
        public int[] GetAllResourceStockInbound() => _resourceStockInbound;
        
#endregion
        
        
#region LifeCycle

        private void Awake()
        {
            _wareDepot = GetComponentInParent<WareDepot>();
            logger.LogObjectAssignmentResult( $"{nameof(_wareDepot)}", _wareDepot );
            
            _wareChannel = GetComponentInParent<WareChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_wareChannel)}", _wareChannel );
            _wareChannel.OnAllWorkersSpawned += OnAllWorkersSpawned;

            _wareStorage = GetComponentInParent<WareStorage>();
            logger.LogObjectAssignmentResult( $"{nameof(_wareStorage)}", _wareStorage );

            _resourceOrderManagerChannel = GetComponent<OrderManagerChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_wareChannel)}", _wareChannel );
            _resourceOrderManagerChannel.OnFreeOrderAvailable += OnFreeOrderAvailable;
            _resourceOrderManagerChannel.OnAllFreeOrdersTaken += OnAllFreeOrdersTaken;

            _orderPanelHandler = GetComponentInChildren<OrderPanelHandler>();
            logger.LogObjectAssignmentResult( nameof(_orderPanelHandler), _orderPanelHandler );
            
            _orderAssignmentManager = GetComponentInParent<OrderAssignmentManager>();
            logger.LogObjectAssignmentResult( $"{nameof(_orderAssignmentManager)}", _orderAssignmentManager );

            _dockingModule = GetComponentInChildren<DockingModule>();
            logger.LogObjectAssignmentResult( $"{nameof(_dockingModule)}", _dockingModule );

            _workerManager = GetComponentInChildren<WorkerManager>();
            logger.LogObjectAssignmentResult( $"{nameof(_workerManager)}", _workerManager );
            
            _resourceOrderManagerChannel.OnOrderComplete += OnOrderComplete;
            _resourceOrderManagerChannel.OnOrderCanceled += OnOrderCanceled;
            
            InitializeProductionStateMachine();
        }

        private void Update()
        {
            _orderManagementStateMachine.Tick();
        }

        private void OnFreeOrderAvailable() => _freeOrderAvailable = true;
        private void OnAllFreeOrdersTaken() => _freeOrderAvailable = false;

#endregion
        
        

#region Initialization

        public void AddWorker( Worker ship ) => _workerManager.AddWorker( ship );

        private void OnAllWorkersSpawned( Ware productWare )
        {
            logger.LogStart();

            int totalWorkersSpawned = _workerManager.GetTotalWorkerCount();
            _resourceOrderHandler = new OrderHandler( _resourceOrderManagerChannel, _wareDepot, 
                totalWorkersSpawned, _orderAssignmentManager.GetScansBeforeCancel(), orderHandlerLogger, orderLogger );
            _resourceStockInbound = new int[productWare.GetNumberOfResources()];

            _freeOrderAvailable = _resourceOrderHandler.OrderIsAvailable();

            string result = _freeOrderAvailable ? GetColoredStringGreenYellow( "DOES" ) : GetColoredStringFireBrick( "DOES NOT" );
            logger.LogEnd( $"{GetColoredStringYellow( _wareDepot.GetHierarchyName() )} {result} start with free orders available.");
        }
        
        private void InitializeProductionStateMachine()
        {
            // State Machine
            _orderManagementStateMachine = new StateMachine();

            // States
            var replenishingStock = new ReplenishingStock( this, _wareStorage, stockScanDelay, logger, _resourceOrderManagerChannel );
            var idle = new IdleManager( logger );

            idle.OnIdleStarted += OnIdleStarted;
            
            // Transitions
            void AddTrans( IState from, IState to, Func<bool> condition ) => _orderManagementStateMachine.AddTransition( from, to, condition );

            AddTrans( idle, replenishingStock, CanPlaceOrders );
            AddTrans( replenishingStock, idle, CanNotPlaceOrders );

            // Default state
            _orderManagementStateMachine.SetState( idle );
        }

        private void OnIdleStarted() => _resourceOrderManagerChannel.OnOrderManagerStatusUpdatedNotify( "Stock is Full or All Orders In Use" );


        private bool CanPlaceOrders() => !ResourceStockIsFull() && _freeOrderAvailable;
        private bool CanNotPlaceOrders() => ResourceStockIsFull() || !_freeOrderAvailable;

        /// <summary> Returns true if at least one resource storage isn't full. </summary>
        private bool ResourceStockIsFull()
        {
            for (int i = 0; i < _wareDepot.GetProduct().GetNumberOfResources(); i++)
            {
                int storageAvailable = _wareStorage.GetRemainingStorageSpaceForResource( i );
                int stockVsInboundDifference = storageAvailable - _resourceStockInbound[i];
                if ( stockVsInboundDifference > 0 ) return false;
            }

            return true;
        }

#endregion
        

#region ResourceOrderManagement

        public bool OrderPanelIsAvailable() => _orderPanelHandler.OrderPanelIsAvailable();
        
        public void PlaceOrderForResource( int wareIndex, int orderAmount )
        {
            logger.LogStart( true );

            logger.Log( $"Placing order for {GetColoredStringYellow( orderAmount.ToString() )} " +
                        $"{GetColoredStringGreen( _wareStorage.GetTypeNameForResource( wareIndex ) )}(s)." );

            Ware ware = _wareStorage.GetResource( wareIndex );
            Order order = _resourceOrderHandler.PlaceOrder( ware, wareIndex, orderAmount );
            order.GetOrderDetailsChannel().OnOderQuantityUpdated += OnOderQuantityUpdated;
            order.GetOrderDetailsChannel().OnWareDelivered += OnWareDelivered;
            _resourceStockInbound[wareIndex] += orderAmount;
            
            if ( _orderPanelHandler != null )
                _orderPanelHandler.RegisterRomOrder( order );

            TriggerSingleResourceUpdatedEvent( wareIndex );

            logger.LogEnd();
        }

        private void OnOderQuantityUpdated( int wareIndex, int amountRemoved )
        {
            logger.LogStart();
            
            // Update inbound stock counts and inform UI elements of the change.
            _resourceStockInbound[wareIndex] -= amountRemoved;
            TriggerSingleResourceUpdatedEvent( wareIndex );

            logger.LogEnd($"Inbound stock for {GetColoredStringYellow( _wareStorage.GetProduct().GetResourceWareForIndex( wareIndex ).ToString() )} " +
                          $"reduced by {GetColoredStringFireBrick( amountRemoved.ToString() )}. " +
                          $"Inbound stock now at {GetColoredStringYellowGreen( _resourceStockInbound[wareIndex].ToString() )}");
        }
        
        
        private void OnOrderCanceled( Order order )
        {
            order.GetOrderDetailsChannel().OnOderQuantityUpdated -= OnOderQuantityUpdated;
            order.GetOrderDetailsChannel().OnWareDelivered -= OnWareDelivered;
            
            int wareIndex = order.GetWareIndex();
            _resourceStockInbound[wareIndex] -= order.GetQuantity();

            TriggerSingleResourceUpdatedEvent( wareIndex );
        }

        /// <summary> Called by order handler when it receives signal from order that it has been completed. Note that the order's signal is triggered by the worker upon
        /// completion of transferring the wares to the destination ware depot. </summary>
        private void OnOrderComplete( Order order )
        {
            order.GetOrderDetailsChannel().OnOderQuantityUpdated -= OnOderQuantityUpdated;
            order.GetOrderDetailsChannel().OnWareDelivered -= OnWareDelivered;
        }

        private void TriggerSingleResourceUpdatedEvent( int wareIndex )
        {
            _wareChannel.OnSingleResourceCountChangedNotify( wareIndex, _wareStorage.GetResourceStock( wareIndex ), 
                _resourceStockInbound[wareIndex],  _wareStorage.GetProduct().GetResourceWareForIndex( wareIndex ).GetDepotResourceStorageSize() );
        }
        
        private void OnWareDelivered( Order order, int amount )
        {
            _resourceStockInbound[order.GetWareIndex()] -= amount;
            _wareStorage.DepositResource( order.GetWare(), amount );
            
            TriggerSingleResourceUpdatedEvent( order.GetWareIndex() );
        }

#endregion
    }
}