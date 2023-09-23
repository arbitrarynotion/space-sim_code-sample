using UnityEngine;
using Packages.com.ianritter.unityscriptingtools.Runtime.Services.CustomLogger;
using SpaceSim.Scripts.Runtime.AI.TaskSystem.EventChannels;
using SpaceSim.Scripts.Runtime.Entities.Factories.Docking;
using SpaceSim.Scripts.Runtime.Entities.Wares;
using SpaceSim.Scripts.Runtime.Entities.Workers;
using static Packages.com.ianritter.unityscriptingtools.Runtime.Services.TextFormatting.TextFormat;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem
{
    /// <summary>
    ///     Factories will hold a pool of these orders. When a need for a ware arises, if an order object is available, it will be filled out
    ///     and posted. It will then be assigned to a worker when one is available.
    /// </summary>
    public class Order
    {
        private readonly WareDepot _homeFactory;
        private readonly OrderChannel _orderChannel;

        // Order details - what is ordered.
        private readonly int _orderId;
        private int _orderNumber;
        private OrderStatus _orderStatus = OrderStatus.Free;
        private string _orderName;
        private Ware _ware;
        private int _wareIndex;
        private int _quantity;

        private int _stockLocationAttempts;
        
        // Order actors - the objects that play a role in filling the order.
        private bool _isAssigned;
        private WareDepot _assignedFactory;
        private DockingBay _pickupDockingBay;
        private Worker _assignedShip;

        private readonly CustomLogger _logger;
        private readonly int _scansBeforeCancel;
        
        
        public Order( int orderId, WareDepot homeFactory, int scansBeforeCancel, CustomLogger logger )
        {
            _orderId = orderId;
            _orderChannel = new OrderChannel();
            _homeFactory = homeFactory;
            _scansBeforeCancel = scansBeforeCancel;
            _logger = logger;
        }

        
#region Events
        
        public void BroadcastUiInitialization() => _orderChannel.OnProductsOrderedUpdatedNotify( _quantity, _ware.GetWareCode(), _ware.GetTypeName() );

        public void BroadcastForUi()
        {
            int quantity = 0;
            string wareCode = "NA";
            string wareTypeName = "NA";
            if ( _ware != null )
            {
                quantity = _quantity;
                wareCode = _ware.GetWareCode();
                wareTypeName = _ware.GetTypeName();
            }
            _orderChannel.OnProductsOrderedUpdatedNotify( quantity, wareCode, wareTypeName );
            _orderChannel.OnWorkerStatusUpdatedNotify( _assignedShip != null ? _assignedShip.GetStatusRom().ToString() : "None" );
        }

        public void BroadcastAllOrderDetails()
        {
            _orderChannel.OnProductsOrderedUpdatedNotify( _quantity, _ware.GetWareCode(), _ware.GetTypeName() );
            _orderChannel.OnActorsAssignedNotify( this, _assignedFactory, _assignedShip );
        }
        
#endregion
        
        
#region Getters

        public int GetOderId() => _orderId;

        public int GetScansBeforeCancel() => _scansBeforeCancel;
        public OrderChannel GetOrderDetailsChannel() => _orderChannel;

        public int GetOrderNumber() => _orderNumber;
        public bool IsAssigned() => _isAssigned;
        
        public bool IsOpen() => _orderStatus == OrderStatus.Open;
        public bool IsFree() => _orderStatus == OrderStatus.Free;

        public string GetOrderName() => $"[{GetColoredStringOrangeRed( _orderNumber.ToString() )}] {_orderName}";
        public WareDepot GetHomeWareDepot() => _homeFactory;
        public Ware GetWare() => _ware;
        public int GetWareIndex() => _wareIndex;
        public int GetQuantity() => _quantity;
        public int GetMinimumQuantityAcceptable() => _ware.GetMinimumOrderAmount();
        public WareDepot GetTargetWareDepot() => _assignedFactory;
        public Worker GetWorker() => _assignedShip;

        public Transform GetPickupPointTransform() => _pickupDockingBay.GetDockingPointTransform();

        public string GetPickupPointName() => _pickupDockingBay.GetDockingBayName();

        public void UpdateOrderQuantity()
        {
            _logger.LogStart();
            
            // Check if the ware depot found has less than the order amount in stock and, if so, adjust the order quantity to that amount.
            int productStockAvailable = _assignedFactory.GetWareStorage().GetAvailableProductStock();
            if ( productStockAvailable >= _quantity )
            {
                _logger.LogEnd();
                return;
            }
            
            _logger.Log( $"Order quantity updated from {GetColoredStringYellow( _quantity.ToString() )} to " +
                        $"{GetColoredStringYellow( productStockAvailable.ToString() )} to match availability." );

            int amountRemoved = _quantity - productStockAvailable;
            _quantity = productStockAvailable;
            
            
            // Notify home ROM that the order quantity has changed so it can remove it from inbound counts.
            _orderChannel.OnProductsOrderedUpdatedNotify( _quantity, _ware.GetWareCode(), _ware.GetTypeName() );
            _orderChannel.OnOderQuantityUpdatedNotify( _wareIndex, amountRemoved );

            _logger.LogEnd();
        }

#endregion
        
        
#region OrderHandling

        public void UpdateAssignmentWaiting( string waitingText ) => _orderChannel.OnAssignmentWaitingUpdatedNotify( waitingText );
        public void UpdateAssignmentStatus( string status ) => _orderChannel.OnAssignmentStatusUpdatedNotify( status );

        public void UpdateAssignmentTimer( int count ) => _orderChannel.OnAssignmentTimerUpdatedNotify( count );

        /// <summary> Increments the attempts count and returns true if more attempts are allowed. Note that this will always returns true
        /// if scans before cancel is set to 0. </summary>
        public bool IsAllowedToRetryAfterCountIncrement()
        {
            if ( _scansBeforeCancel == 0 ) return true;
            
            _stockLocationAttempts++;
            // Debug.Log( $"{GetOrderName()} failed attempts incremented to {GetColoredStringYellow( _stockLocationAttempts.ToString() )}" );
            _orderChannel.OnFailedScansCountUpdatedNotify( _stockLocationAttempts );

            if ( _stockLocationAttempts < _scansBeforeCancel ) return true;

            _stockLocationAttempts = 0;
            CancelOrder();
            return false;
        }
        
        /// <summary> Define what the order is for. This puts the order into Open status where it awaits the assignment of actors via SetOrderActors. </summary>
        public void PopulateOrder( string orderName, int orderNumber, Ware ware, int wareIndex, int quantity )
        {
            _logger.LogStart( false, $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: {GetColoredStringYellow( orderNumber.ToString() )}" );
            
            _orderStatus = OrderStatus.Open;
            _logger.Log( $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: " +
                         $"{GetColoredStringYellow( _orderNumber.ToString() )} status set to " +
                         $"{GetColoredStringGreenYellow( _orderStatus.ToString() )}" );
            
            _orderNumber = orderNumber;
            _orderName = orderName;
            _ware = ware;
            _wareIndex = wareIndex;
            _quantity = quantity;
            _stockLocationAttempts = 0;
            
            _logger.LogEnd();
        }
        
        /// <summary> Set the actors responsible for fulfilling the order. This puts the order into Assigned status and work on completing it should begin. </summary>
        public void SetOrderActors( WareDepot factory, Worker ship )
        {
            _logger.LogStart( false, $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: {GetColoredStringYellow( _orderNumber.ToString() )}" );
            
            _orderStatus = OrderStatus.Assigned;
            _logger.Log( $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: " +
                         $"{GetColoredStringYellow( _orderNumber.ToString() )} status set to " +
                         $"{GetColoredStringGreenYellow( _orderStatus.ToString() )}" );
            
            _isAssigned = true;

            _assignedFactory = factory;
            _assignedShip = ship;
            _assignedShip.GetWorkerChannel().OnDepartedTowardsHome += OnWorkerCollectionCompleted;
            
            _orderChannel.OnActorsAssignedNotify( this, _assignedFactory, _assignedShip );
            
            // BroadcastForUi();
            
            _logger.LogEnd();
        }

        private void OnWorkerCollectionCompleted() => _pickupDockingBay.ReleaseDockingBay();

        public void SetOrderPickupDockingBay( DockingBay pickupDockingBay )
        {
            _pickupDockingBay = pickupDockingBay;
            _orderChannel.OnPickupPointSetNotify( pickupDockingBay.GetDockingBayName() );
        }

        public void WareWasCollected( int amount ) => _orderChannel.OnWareCollectedNotify( this, amount );
        public void WareWasDelivered( int amount ) => _orderChannel.OnWareDeliveredNotify( this, amount );

        public void WareTransferComplete()
        {
            _orderChannel.OnWareTransferCompleteNotify( this );
        }

        /// <summary> Reset the order so that it can be reused. </summary>
        public void CompleteOrder()
        {
            _logger.LogStart( false, $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: {GetColoredStringYellow( _orderNumber.ToString() )}" );

            // Broadcast oder completion signal. This will be picked up by the order handler, which will pass it up 
            // to its order manager.
            _logger.Log( "Broadcasting order complete on Order Details Channel." );
            _orderChannel.OnOrderCompleteNotify( this );

            ResetOrder();
            
            _logger.LogEnd();
        }
        
        /// <summary> Reset the order so that it can be reused. </summary>
        private void CancelOrder()
        {
            _logger.LogStart( false, $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: {GetColoredStringYellow( _orderNumber.ToString() )}" );

            // Broadcast oder completion signal. This will be picked up by the order handler, which will pass it up 
            // to its order manager.
            _logger.Log( "Broadcasting order canceled on Order Details Channel." );
            _orderChannel.OnOrderCanceledNotify( this );

            ResetOrder();
            
            _logger.LogEnd();
        }

        private void ResetOrder()
        {
            _logger.LogStart( false, $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: {GetColoredStringYellow( _orderNumber.ToString() )}" );

            // Reset order details.
            _logger.Log( "Resetting order details." );
            _orderName = string.Empty;
            _ware = null;
            _quantity = 0;
            _stockLocationAttempts = 0;
            
            // Clear actors.
            _logger.Log( "Clearing actors and unsubscribing from worker channel." );
            _isAssigned = false;
            _assignedFactory = null;
            if ( _assignedShip != null )
            {
                _assignedShip.GetWorkerChannel().OnDepartedTowardsHome -= OnWorkerCollectionCompleted;
                _assignedShip = null;
            }
            _pickupDockingBay = null;
            
            _orderStatus = OrderStatus.Free;
            
            _logger.Log( $"Order [{GetColoredStringOrange( _orderId.ToString() )}]: " +
                         $"{GetColoredStringYellow( _orderNumber.ToString() )} status set to " +
                         $"{GetColoredStringGreenYellow( _orderStatus.ToString() )}" );
            
            _logger.Log( "Broadcasting update for UI." );
            BroadcastForUi();
            _logger.LogEnd();
        }

#endregion
    }
}