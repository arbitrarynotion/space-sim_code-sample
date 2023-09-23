using Packages.com.ianritter.unityscriptingtools.Runtime.Services.CustomLogger;
using SpaceSim.Scripts.Runtime.Entities.Factories.Docking;
using SpaceSim.Scripts.Runtime.Entities.Wares;
using SpaceSim.Scripts.Runtime.UI.Entities.Factories;
using UnityEngine;
using static Packages.com.ianritter.unityscriptingtools.Runtime.Services.TextFormatting.TextFormat;

namespace SpaceSim.Scripts.Runtime.AI.TaskSystem.Managers
{
    public class ProductOrderManager : MonoBehaviour
    {
        [SerializeField] private CustomLogger logger;
        
        private WareDepot _wareDepot;
        private WareStorage _wareStorage;
        private WareChannel _wareChannel;
        private OrderPanelHandler _orderPanelHandler;
        private DockingModule _dockingModule;

        private int _maxConcurrentOrders;
        private int _productDepotStorageSize;
        private Order[] _productOrderReferences;
        private int _productStockReserved = 0;
        
#region LifeCycle

        private void Awake()
        {
            _wareDepot = GetComponentInParent<WareDepot>();
            logger.LogObjectAssignmentResult( nameof(_wareDepot), _wareDepot );
            
            _wareStorage = GetComponentInParent<WareStorage>();
            logger.LogObjectAssignmentResult( nameof(_wareStorage), _wareStorage );
            
            _wareChannel = GetComponentInParent<WareChannel>();
            logger.LogObjectAssignmentResult( $"{nameof(_wareChannel)}", _wareChannel );
            _wareChannel.OnProductWareSet += OnProductWareAssigned;
            
            _dockingModule = GetComponentInChildren<DockingModule>();
            logger.LogObjectAssignmentResult( nameof(_dockingModule), _dockingModule );
            _maxConcurrentOrders = _dockingModule.GetDockingBayCount();
            
            _orderPanelHandler = GetComponentInChildren<OrderPanelHandler>();
            logger.LogObjectAssignmentResult( nameof(_orderPanelHandler), _orderPanelHandler );
        }
        
#endregion
        
        
#region Initialization
        
        private void OnProductWareAssigned( Ware productWare )
        {
            logger.LogStart();
            
            _productStockReserved = 0;
            _productOrderReferences = new Order[_maxConcurrentOrders];
            _productDepotStorageSize = _wareStorage.GetProductDepotStorageSize();

            logger.LogEnd();
        }

#endregion
        
        
#region Events
        
        private void OnWareCollected( Order order, int amount )
        {
            _productStockReserved -= amount;
            _wareStorage.WithdrawProduct( amount );
        }
        
#endregion
        
        
#region Getters

        public int GetProductStockReserved() => _productStockReserved;
        public bool HasPendingOrders() => _productStockReserved > 0;
        
#endregion

        
#region OrderHandling

        /// <summary> Returns true if an order slot is available. </summary>
        public bool IsOpenForBusiness()
        {
            logger.LogStart();
            int index = GetAvailableOrderRefSlotIndex();
            logger.Log( $"Available index scan returned {GetColoredStringYellow( index.ToString() )}." );
            logger.LogEnd();
            return index >= 0;
        }

        /// <summary> Returns true if there is room for more of this ware, taking pending orders into consideration.</summary>
        private int GetAvailableStock()
        {
            int availableStock = _wareDepot.GetWareStorage().GetAvailableProductStock() - _productStockReserved;

            return availableStock > 0 ? availableStock : 0;
        }

        /// <summary> Attempt to register a product order. Order must already have destination depot and ship assigned. Returns false if no order slots are available. </summary>
        public bool RegisterProductOrder( Order order )
        {
            logger.LogStart( true, $"Registering order for {GetColoredStringYellow( order.GetQuantity().ToString() )} " +
                                   $"{GetColoredStringGreen( _wareDepot.GetWareStorage().GetProduct().GetTypeName() )}(s) " +
                                   $"from {GetColoredStringYellow( order.GetHomeWareDepot().GetDisplayName() )}." );
            
            int availableIndex = GetAvailableOrderRefSlotIndex();
            if ( availableIndex < 0 )
            {
                logger.LogEnd( "Warning! No product order slots are available!" );
                return false;
            }

            // Get available product docking bay.
            DockingBay dockingBay = _dockingModule.GetFreeDockingBay();
            if ( dockingBay == null )
            {
                logger.LogEnd( "Warning! No product docking bays are available!" );
                return false;
            }

            RegisterOrderForProduct( availableIndex, order, dockingBay );
            
            logger.LogEnd( $"{GetColoredStringYellow( order.GetOrderName() )} order for " +
                           $"{GetColoredStringYellow( order.GetQuantity().ToString() )} {GetColoredStringYellow( order.GetWare().GetTypeName() )}(s) successfully registered. " +
                           $"Total products reserved: {GetColoredStringGreenYellow( _productStockReserved.ToString() )} {_wareDepot.GetProduct().GetTypeName()}." );
            return true;
        }

        private void RegisterOrderForProduct( int availableIndex, Order order, DockingBay dockingBay )
        {
            logger.LogStart();

            dockingBay.ReserveDockingBay( order.GetWorker() );
            if ( _orderPanelHandler != null )
            {
                _orderPanelHandler.RegisterPomOrder( order );
                order.BroadcastForUi();
            }
            order.SetOrderPickupDockingBay( dockingBay );
            order.GetOrderDetailsChannel().OnOrderComplete += OnProductOrderCompleted;
            order.GetOrderDetailsChannel().OnWareCollected += OnWareCollected;
            _productOrderReferences[availableIndex] = order;
            _productStockReserved += order.GetQuantity();

            
            // Update progress bar overlay to reflect products currently reserved.
            _wareChannel.OnProductCountChangedNotify( _wareStorage.GetRawProductStock(), _productStockReserved, _productDepotStorageSize );

            logger.LogEnd();
        }

        private int GetAvailableOrderRefSlotIndex()
        {
            for (int i = 0; i < _productOrderReferences.Length; i++)
            {
                if ( _productOrderReferences[i] == null ) return i;
            }

            return -1;
        }

        /// <summary> Called every time an order is completed on the order channel. </summary>
        private void OnProductOrderCompleted( Order order )
        {
            logger.LogStart();

            ClearOrderIfInProductOrderRefs( order );
            
            logger.LogEnd( $"{GetColoredStringYellow( order.GetOrderName() )} completed. " +
                           $"Total products reserved: {GetColoredStringGreenYellow( _productStockReserved.ToString() )} {_wareDepot.GetProduct().GetTypeName()}." );
        }

        /// <summary> Returns true if the order was found and removed. </summary>
        private bool ClearOrderIfInProductOrderRefs( Order order )
        {
            order.GetOrderDetailsChannel().OnOrderComplete -= OnProductOrderCompleted;
            order.GetOrderDetailsChannel().OnWareCollected -= OnWareCollected;
            for (int i = 0; i < _productOrderReferences.Length; i++)
            {
                if ( _productOrderReferences[i] != order ) continue;
                
                _productOrderReferences[i] = null;
                return true;
            }

            return false;
        }

#endregion
    }
}
